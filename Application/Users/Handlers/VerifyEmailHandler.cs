using Application.Users.Commands;
using Application.Users.Ports;
using Application.Users.Tokens;
using Domain.Users;
using SharedKernel;

namespace Application.Users.Handlers;

/// <summary>
/// Confirm an email address from the token in the verification link, then issue a token.
/// <para>
/// The presented token is hashed before it reaches the domain, because the domain only ever holds
/// a <c>TokenHash</c>. Comparing the stored hash against the incoming hash also means the
/// comparison is over two fixed-length hex strings, so <c>UserTransitions.Verify</c> gets a
/// plain equality check with no early-exit timing channel.
/// </para>
/// </summary>
public sealed class VerifyEmailHandler(
    IUserRepository users,
    IAccessTokenIssuer tokenIssuer,
    TimeProvider timeProvider)
    : ICommandHandler<VerifyEmailCommand, Result<SignInResult>>
{
    public async Task<Result<SignInResult>> Handle(
        VerifyEmailCommand command,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        var email = Email.Create(command.Email);
        if (!email.IsSuccess)
            return Result.Failure<SignInResult>(email.Error);

        var presented = TokenHash.Create(TokenHasher.Hash(command.Token));
        if (!presented.IsSuccess)
            return Result.Failure<SignInResult>(Error.Validation(
                "Auth.Verification.InvalidToken",
                "The verification token is not valid."));

        var user = await users.FindByEmailAsync(email.Value, ct);

        // Same rule as sign-in: an unknown address and a wrong token must not be distinguishable.
        if (user is not PendingVerification pending)
            return Result.Failure<SignInResult>(Error.Validation(
                "Auth.Verification.InvalidToken",
                "The verification link is not valid."));

        var verified = UserTransitions.Verify(pending, presented.Value, now);
        if (!verified.IsSuccess)
            return Result.Failure<SignInResult>(verified.Error);

        var token = tokenIssuer.Issue(verified.Value.Id, [.. verified.Value.Roles.AsEnumerable()]);

        await users.SaveAsync(verified.Value, ct);

        return Result.Success(new SignInResult(token.Token, token.ExpiresAt, verified.Value.Email.Value));
    }

}