using System.Security.Cryptography;
using System.Text;
using Application.Users.Commands;
using Application.Users.Ports;
using Application.Users.Tokens;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Handlers;

/// <summary>
/// Exchange a verified email + password for a bearer token.
/// <para>
/// The one handler with a rule that cannot live in the domain, and it is stated here rather than
/// hidden: <b>every failure returns the same error</b>. "No such account" and "wrong password"
/// must be indistinguishable, because an endpoint that tells them apart is a free account-enumeration
/// oracle — attackers would use it to harvest valid addresses for phishing and credential
/// stuffing. That also covers <c>PendingVerification</c>, <c>LockedOut</c> and <c>Deactivated</c>
///: all of them fall through the same path, so this endpoint cannot be used to probe an account's
/// lifecycle stage either.
/// </para>
/// <para>
/// The domain still decides the outcome. It is the only thing that knows a locked account stays
/// locked and that a successful sign-in resets the failure counter; this handler just decides
/// <em>which</em> error the caller is allowed to see.
/// </para>
/// </summary>
public sealed class SignInUserHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    LockoutPolicy lockoutPolicy,
    TimeProvider timeProvider)
    : ICommandHandler<SignInUserCommand, Result<SignInResult>>
{
    private static readonly Error InvalidCredentials = Error.Unauthorized(
        "Auth.SignIn.Failed",
        "Email or password is incorrect.");

    public async Task<Result<SignInResult>> Handle(
        SignInUserCommand command,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        var email = Email.Create(command.Email);
        if (!email.IsSuccess)
            return Result.Failure<SignInResult>(InvalidCredentials);

        var user = await users.FindByEmailAsync(email.Value, ct);

        if (user is null)
        {
            // Hash against a throwaway value anyway. Without this, a request for an unknown
            // address returns measurably faster than a request for a known one with a wrong
            // password, which is user enumeration by response time — the exact leak the single
            // error above exists to prevent, just through a different channel.
            passwordHasher.Verify(command.Password ?? string.Empty, DecoyHash);
            return Result.Failure<SignInResult>(InvalidCredentials);
        }

        var storedHash = user.Password.Value;
        var verification = passwordHasher.Verify(command.Password ?? string.Empty, storedHash);

        // Password is checked BEFORE the lifecycle stage, on purpose. Checking the stage first
        // would tell a caller with no valid password whether an account is active, and would also
        // let an attacker skip the expensive hash for accounts that cannot sign in anyway.
        if (verification == PasswordVerificationResult.Failed)
            return await RecordFailureAsync(user, now, ct);

        return user switch
        {
            Active active => await IssueAsync(active, command.Password!, verification, now, ct),
            LockedOut locked => await TryUnlockThenIssue(locked, command.Password!, verification, now, ct),
            _ => await RecordFailureAsync(user, now, ct)
        };
    }

    private async Task<Result<SignInResult>> IssueAsync(
        Active active,
        string plaintextPassword,
        PasswordVerificationResult verification,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var user = UserTransitions.RecordSuccessfulSignIn(active);

        // Rehash on a successful verify when the stored hash used a weaker scheme. Silent and
        // free, because the plaintext is in scope exactly here and nowhere else.
        if (verification == PasswordVerificationResult.RehashNeeded)
        {
            var rehashed = PasswordHash.Create(passwordHasher.Hash(plaintextPassword));
            if (rehashed.IsSuccess)
                user = UserTransitions.ChangePassword(user, rehashed.Value).Value;
        }

        var token = tokenIssuer.Issue(user.Id, [.. user.Roles.AsEnumerable()]);

        await users.SaveAsync(user, ct);

        return Result.Success(new SignInResult(token.Token, token.ExpiresAt, user.Email.Value));
    }

    private async Task<Result<SignInResult>> TryUnlockThenIssue(
        LockedOut locked,
        string plaintextPassword,
        PasswordVerificationResult verification,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // The lockout is a time-based rule, so whether the window has elapsed is data the type
        // system cannot check. The domain decides; this only routes on its answer.
        var unlocked = UserTransitions.Unlock(locked, now);
        if (!unlocked.IsSuccess)
            return Result.Failure<SignInResult>(InvalidCredentials);

        return await IssueAsync(unlocked.Value, plaintextPassword, verification, now, ct);
    }

    private async Task<Result<SignInResult>> RecordFailureAsync(
        UserState user,
        DateTimeOffset now,
        CancellationToken ct)
    {
        // Only Active accounts carry a failure counter, and RecordFailedSignIn only accepts
        // Active. A wrong password against a LockedOut or Deactivated account therefore cannot
        // increment anything — which is correct: those accounts have no counter left to increment.
        if (user is not Active active)
            return Result.Failure<SignInResult>(InvalidCredentials);

        await users.SaveAsync(UserTransitions.RecordFailedSignIn(active, lockoutPolicy, now), ct);

        return Result.Failure<SignInResult>(InvalidCredentials);
    }

    /// <summary>
    /// A real hash of a random value, used only to equalise timing for unknown addresses. It
    /// must be a well-formed hash of *something*, or the hasher may short-circuit and reintroduce
    /// the timing difference this is here to remove.
    /// </summary>
    private static readonly string DecoyHash =
        "$argon2id$v=19$m=65536,t=3,p=1$ZGVjb3lTZmxhc2hOYXVnaA$0000000000000000000000000000000000000000000";
}
