using SharedKernel;

namespace Domain.Users;

/// <summary>
/// The user account lifecycle, as types rather than as a status column.
///
/// Why a hierarchy and not an enum plus nullable fields: the legal operations differ per
/// stage. A PendingVerification account has no way to sign in (no transition takes it), a
/// LockedOut account has no verification token left to re-send, and a Deactivated account
/// cannot grant roles. With a single class plus a <c>UserStatus</c> enum, every one of those
/// rules becomes an <c>if</c> that a caller can forget, and the compiler catches none of them.
///
/// The stages here come from four events: <em>user registered</em>, <em>email verified</em>,
/// <em>account locked</em>, <em>account deactivated</em>. If a fifth stage ever appears it
/// needs a fifth event behind it, not just a new enum member.
/// </summary>
public abstract record UserState
{
    public required UserId Id { get; init; }

    public required Email Email { get; init; }

    public required PasswordHash Password { get; init; }

    public required UserRoles Roles { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}

/// <summary>Registered, but the email address has not been proven yet. Cannot sign in.</summary>
public sealed record PendingVerification : UserState
{
    public required TokenHash VerificationToken { get; init; }

    public required DateTimeOffset VerificationTokenExpiresAt { get; init; }
}

/// <summary>Email proven. The only state a sign-in attempt can start from.</summary>
public sealed record Active : UserState
{
    public required DateTimeOffset EmailVerifiedAt { get; init; }

    public required int FailedSignInAttempts { get; init; }
}

/// <summary>Too many failed sign-ins. Cannot sign in until the lockout window has passed.</summary>
public sealed record LockedOut : UserState
{
    public required DateTimeOffset EmailVerifiedAt { get; init; }

    /// <summary>Kept only to render to the user ("try again in 12 minutes") — not a security input.</summary>
    public required int FailedSignInAttempts { get; init; }

    public required DateTimeOffset LockedAt { get; init; }

    public required DateTimeOffset LockedUntil { get; init; }
}

/// <summary>Disabled by an administrator. Terminal until explicitly reactivated.</summary>
public sealed record Deactivated : UserState
{
    public required DateTimeOffset EmailVerifiedAt { get; init; }

    public required DateTimeOffset DeactivatedAt { get; init; }

    public required string DeactivationReason { get; init; }
}
