namespace Infrastructure.Persistence.Entities;

/// <summary>
/// The flat database row for a user account. One row per account, one status column, and a
/// nullable column for each stage-specific value.
/// <para>
/// The nullable columns are a faithful picture of the domain's
/// <c>PendingVerification | Active | LockedOut | Deactivated</c> hierarchy rather than a
/// shortcut around it: the row shape is what a relational table can honestly express, and
/// <c>UserMapper</c> is the single place that knows which combination of nulls means which
/// sealed record. See <see cref="UserStatus"/>.
/// </para>
/// <para>
/// Mutable by design — this is a persistence model, not a domain type. It is never handed to a
/// caller.
/// </para>
/// </summary>
public class UserEntity
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public UserStatus Status { get; set; } = UserStatus.PendingVerification;

    /// <summary>Serialized JSON array of role names. Small, write-rarely, never queried by role.</summary>
    public required string RolesJson { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    // ---- PendingVerification ----

    public string? VerificationTokenHash { get; set; }

    public DateTimeOffset? VerificationTokenExpiresAt { get; set; }

    // ---- Active ----

    public DateTimeOffset? EmailVerifiedAt { get; set; }

    public int FailedSignInAttempts { get; set; }

    // ---- LockedOut ----

    public DateTimeOffset? LockedAt { get; set; }

    public DateTimeOffset? LockedUntil { get; set; }

    // ---- Deactivated ----

    public DateTimeOffset? DeactivatedAt { get; set; }

    public string? DeactivationReason { get; set; }
}
