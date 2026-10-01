using System.Collections.Immutable;
using SharedKernel;

namespace Domain.Users;

/// <summary>
/// Every rule that changes a user's lifecycle stage, as a static method whose parameter is
/// the *specific* state it may be called from.
///
/// The reason for that shape: <c>UserTransitions.Deactivate(Active, ...)</c> and
/// <c>UserTransitions.Deactivate(LockedOut, ...)</c> are separate overloads, so
/// "deactivate a Deactivated account" is not a runtime check someone forgot to write — it
/// does not compile. That removes the need for unit tests on most transitions; the tests
/// that remain are for the rules the type system genuinely cannot express (a token that is
/// correct but expired, a lockout window that has not elapsed yet).
/// </summary>
public static class UserTransitions
{
    private static readonly TimeSpan DefaultVerificationWindow = TimeSpan.FromDays(2);

    /// <summary>User registered. The first event; everything else is reachable from here.</summary>
    public static PendingVerification Register(
        Email email,
        PasswordHash password,
        TokenHash verificationToken,
        DateTimeOffset now,
        IEnumerable<Role>? roles = null) =>
        new()
        {
            Id = UserId.New(),
            Email = email,
            Password = password,
            Roles = UserRoles.Create(roles),
            RegisteredAt = now,
            VerificationToken = verificationToken,
            VerificationTokenExpiresAt = now.Add(DefaultVerificationWindow)
        };

    /// <summary>Re-issues the verification token for an account that never confirmed its email.</summary>
    public static Result<PendingVerification> ResendVerificationToken(
        PendingVerification user,
        TokenHash newToken,
        DateTimeOffset now)
    {
        // The rule that needs a runtime check: the caller may pass a user whose previous
        // token is still valid, and a second live token would let anyone who intercepted
        // the first email still activate the account.
        if (now < user.VerificationTokenExpiresAt)
            return Result.Failure<PendingVerification>(Error.Conflict(
                "User.VerificationToken.StillValid",
                "The current verification link is still valid; resend is not allowed yet."));

        return Result.Success(new PendingVerification
        {
            Id = user.Id,
            Email = user.Email,
            Password = user.Password,
            Roles = user.Roles,
            RegisteredAt = user.RegisteredAt,
            VerificationToken = newToken,
            VerificationTokenExpiresAt = now.Add(DefaultVerificationWindow)
        });
    }

    /// <summary>Email proven. The account becomes able to sign in for the first time.</summary>
    public static Result<Active> Verify(
        PendingVerification user,
        TokenHash presentedToken,
        DateTimeOffset now)
    {
        if (now > user.VerificationTokenExpiresAt)
            return Result.Failure<Active>(Error.Validation(
                "User.VerificationToken.Expired",
                "The verification link has expired. Request a new one."));

        if (!presentedToken.Equals(user.VerificationToken))
            return Result.Failure<Active>(Error.Validation(
                "User.VerificationToken.Invalid",
                "The verification token is not valid for this account."));

        return Result.Success(new Active
        {
            Id = user.Id,
            Email = user.Email,
            Password = user.Password,
            Roles = user.Roles,
            RegisteredAt = user.RegisteredAt,
            EmailVerifiedAt = now,
            FailedSignInAttempts = 0
        });
    }

    /// <summary>
    /// A sign-in attempt failed. Returns <c>Active</c> again below the threshold and
    /// <c>LockedOut</c> at or above it — the union is the point, which is why the return
    /// type is the abstract base rather than one concrete state.
    /// </summary>
    public static UserState RecordFailedSignIn(Active user, LockoutPolicy policy, DateTimeOffset now)
    {
        var attempts = user.FailedSignInAttempts + 1;

        if (policy.ShouldLockOut(attempts))
            return new LockedOut
            {
                Id = user.Id,
                Email = user.Email,
                Password = user.Password,
                Roles = user.Roles,
                RegisteredAt = user.RegisteredAt,
                EmailVerifiedAt = user.EmailVerifiedAt,
                FailedSignInAttempts = attempts,
                LockedAt = now,
                LockedUntil = now.Add(policy.LockoutDuration)
            };

        return user with { FailedSignInAttempts = attempts };
    }

    /// <summary>
    /// A sign-in succeeded, so the counter starts again from zero. No timestamp is recorded:
    /// "last seen" is audit data owned by the sign-in log, not a field of the account — a
    /// nullable <c>LastSignInAt</c> here would be the field-only-in-some-states shape the
    /// state hierarchy exists to avoid.
    /// </summary>
    public static Active RecordSuccessfulSignIn(Active user) => user with { FailedSignInAttempts = 0 };

    /// <summary>
    /// The lockout window has elapsed. Still a runtime check because time is data: a caller
    /// holding a <c>LockedOut</c> can call this an hour early, and the type system cannot
    /// know what time it is.
    /// </summary>
    public static Result<Active> Unlock(LockedOut user, DateTimeOffset now) =>
        now < user.LockedUntil
            ? Result.Failure<Active>(Error.Failure(
                "User.LockedOut.WindowNotElapsed",
                $"The account is locked until {user.LockedUntil:u}."))
            : Result.Success(new Active
            {
                Id = user.Id,
                Email = user.Email,
                Password = user.Password,
                Roles = user.Roles,
                RegisteredAt = user.RegisteredAt,
                EmailVerifiedAt = user.EmailVerifiedAt,
                FailedSignInAttempts = 0
            });

    public static Deactivated Deactivate(Active user, string reason, DateTimeOffset now) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Password = user.Password,
        Roles = user.Roles,
        RegisteredAt = user.RegisteredAt,
        EmailVerifiedAt = user.EmailVerifiedAt,
        DeactivatedAt = now,
        DeactivationReason = reason
    };

    public static Deactivated Deactivate(LockedOut user, string reason, DateTimeOffset now) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Password = user.Password,
        Roles = user.Roles,
        RegisteredAt = user.RegisteredAt,
        EmailVerifiedAt = user.EmailVerifiedAt,
        DeactivatedAt = now,
        DeactivationReason = reason
    };

    public static Deactivated Deactivate(PendingVerification user, string reason, DateTimeOffset now) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Password = user.Password,
        Roles = user.Roles,
        RegisteredAt = user.RegisteredAt,
        EmailVerifiedAt = user.RegisteredAt,
        DeactivatedAt = now,
        DeactivationReason = reason
    };

    /// <summary>Reactivating returns to <c>Active</c>: the email was already proven, so
    /// re-verification is not required and the failed-attempt count starts clean.</summary>
    public static Active Reactivate(Deactivated user, DateTimeOffset now) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Password = user.Password,
        Roles = user.Roles,
        RegisteredAt = user.RegisteredAt,
        EmailVerifiedAt = user.EmailVerifiedAt,
        FailedSignInAttempts = 0
    };

    /// <summary>A password change is only possible while signed in, so it takes <c>Active</c> only.</summary>
    public static Result<Active> ChangePassword(Active user, PasswordHash newPassword)
    {
        if (newPassword.Equals(user.Password))
            return Result.Failure<Active>(Error.Validation(
                "User.Password.Unchanged",
                "The new password must differ from the current one."));

        return Result.Success(user with { Password = newPassword });
    }

    public static Result<Active> GrantRole(Active user, Role role) =>
        user.Roles.Contains(role)
            ? Result.Failure<Active>(Error.Conflict(
                "User.Role.AlreadyGranted",
                $"The account already holds the '{role}' role."))
            : Result.Success(user with { Roles = user.Roles.With(role) });

    public static Result<Active> RevokeRole(Active user, Role role) =>
        !user.Roles.Contains(role)
            ? Result.Failure<Active>(Error.Conflict(
                "User.Role.NotGranted",
                $"The account does not hold the '{role}' role."))
            : Result.Success(user with { Roles = user.Roles.Without(role) });
}
