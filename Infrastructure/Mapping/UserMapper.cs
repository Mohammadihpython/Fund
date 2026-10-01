using System.Text.Json;
using Domain.Users;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Mapping;

/// <summary>
/// The one and only translation point between the flat <see cref="UserEntity"/> row and the
/// domain's sealed-record state hierarchy.
/// <para>
/// Both directions switch over the status, and both are exhaustive on purpose: the
/// <c>CS8509</c>/<c>CS8524</c> warnings-as-errors in <c>Domain.csproj</c> is a promise, and this
/// is where a new <c>UserState</c> would break it. A second status switch anywhere else in the
/// solution is a bug, not a style violation.
/// </para>
/// <para>
/// Reconstruction uses <c>FromTrustedSource</c> rather than <c>Create</c>, and that is the whole
/// reason those methods exist. The data came out of a column that was written by
/// <c>ApplyToEntity</c> from a domain object that was already valid; re-running the smart
/// constructor's checks would be re-validating a guarantee the type already makes, and would
/// turn a corrupt row into a runtime exception during a query rather than a constraint
/// violation on write.
/// </para>
/// </summary>
public static class UserMapper
{
    private static readonly JsonSerializerOptions RoleJson = new(JsonSerializerDefaults.Web);

    public static UserState FromPersistence(UserEntity row) => row.Status switch
    {
        UserStatus.PendingVerification => new PendingVerification
        {
            Id = UserId.FromTrustedSource(row.Id),
            Email = Email.FromTrustedSource(row.Email),
            Password = PasswordHash.FromTrustedSource(row.PasswordHash),
            Roles = ReadRoles(row.RolesJson),
            RegisteredAt = row.RegisteredAt,
            VerificationToken = TokenHash.FromTrustedSource(Required(row.VerificationTokenHash, nameof(row.VerificationTokenHash))),
            VerificationTokenExpiresAt = Required(row.VerificationTokenExpiresAt, nameof(row.VerificationTokenExpiresAt))
        },

        UserStatus.Active => new Active
        {
            Id = UserId.FromTrustedSource(row.Id),
            Email = Email.FromTrustedSource(row.Email),
            Password = PasswordHash.FromTrustedSource(row.PasswordHash),
            Roles = ReadRoles(row.RolesJson),
            RegisteredAt = row.RegisteredAt,
            EmailVerifiedAt = Required(row.EmailVerifiedAt, nameof(row.EmailVerifiedAt)),
            FailedSignInAttempts = row.FailedSignInAttempts
        },

        UserStatus.LockedOut => new LockedOut
        {
            Id = UserId.FromTrustedSource(row.Id),
            Email = Email.FromTrustedSource(row.Email),
            Password = PasswordHash.FromTrustedSource(row.PasswordHash),
            Roles = ReadRoles(row.RolesJson),
            RegisteredAt = row.RegisteredAt,
            EmailVerifiedAt = Required(row.EmailVerifiedAt, nameof(row.EmailVerifiedAt)),
            FailedSignInAttempts = row.FailedSignInAttempts,
            LockedAt = Required(row.LockedAt, nameof(row.LockedAt)),
            LockedUntil = Required(row.LockedUntil, nameof(row.LockedUntil))
        },

        UserStatus.Deactivated => new Deactivated
        {
            Id = UserId.FromTrustedSource(row.Id),
            Email = Email.FromTrustedSource(row.Email),
            Password = PasswordHash.FromTrustedSource(row.PasswordHash),
            Roles = ReadRoles(row.RolesJson),
            RegisteredAt = row.RegisteredAt,
            EmailVerifiedAt = Required(row.EmailVerifiedAt, nameof(row.EmailVerifiedAt)),
            DeactivatedAt = Required(row.DeactivatedAt, nameof(row.DeactivatedAt)),
            DeactivationReason = Required(row.DeactivationReason, nameof(row.DeactivationReason))
        },

        // Reachable only if a value is written to the column that the enum does not know —
        // a hand-edited row, or a status added by a newer build of the app than this one.
        // Failing loudly is right: silently defaulting to Active would hand out a usable
        // session to an account whose real state we do not know.
        _ => throw new UnknownUserStatusException(row.Status, row.Id)
    };

    /// <summary>
    /// Mirror image of <see cref="FromPersistence"/>. Clears every stage-specific column before
    /// writing, so a row that moves Active → LockedOut → Active does not keep a stale
    /// <c>LockedUntil</c> sitting in a column that is <c>NULL</c> for the current status.
    /// </summary>
    public static void ApplyToEntity(UserState state, UserEntity target)
    {
        target.Id = state.Id.Value;
        target.Email = state.Email.Value;
        target.PasswordHash = state.Password.Value;
        target.RolesJson = WriteRoles(state.Roles);
        target.RegisteredAt = state.RegisteredAt;

        target.VerificationTokenHash = null;
        target.VerificationTokenExpiresAt = null;
        target.EmailVerifiedAt = null;
        target.LockedAt = null;
        target.LockedUntil = null;
        target.DeactivatedAt = null;
        target.DeactivationReason = null;

        switch (state)
        {
            case PendingVerification pending:
                target.Status = UserStatus.PendingVerification;
                target.VerificationTokenHash = pending.VerificationToken.Value;
                target.VerificationTokenExpiresAt = pending.VerificationTokenExpiresAt;
                break;

            case Active active:
                target.Status = UserStatus.Active;
                target.EmailVerifiedAt = active.EmailVerifiedAt;
                target.FailedSignInAttempts = active.FailedSignInAttempts;
                break;

            case LockedOut locked:
                target.Status = UserStatus.LockedOut;
                target.EmailVerifiedAt = locked.EmailVerifiedAt;
                target.FailedSignInAttempts = locked.FailedSignInAttempts;
                target.LockedAt = locked.LockedAt;
                target.LockedUntil = locked.LockedUntil;
                break;

            case Deactivated deactivated:
                target.Status = UserStatus.Deactivated;
                target.EmailVerifiedAt = deactivated.EmailVerifiedAt;
                target.DeactivatedAt = deactivated.DeactivatedAt;
                target.DeactivationReason = deactivated.DeactivationReason;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    $"Unhandled user lifecycle state '{state.GetType().Name}'. Add it to UserMapper and UserStatus.");
        }
    }

    // FromTrustedSource, not Create: these role names came out of a column this same mapper
    // wrote from an already-validated set. Re-parsing them through the smart constructor would
    // be re-checking a guarantee the type already carries.
    private static UserRoles ReadRoles(string json) =>
        JsonSerializer.Deserialize<string[]>(json, RoleJson) is { } names
            ? UserRoles.FromTrustedSource(names.Select(Role.FromTrustedSource))
            : UserRoles.Empty;

    private static string WriteRoles(UserRoles roles) =>
        JsonSerializer.Serialize(roles.AsEnumerable().Select(r => r.Value).ToArray(), RoleJson);

    private static T Required<T>(T? value, string column) where T : struct =>
        value ?? throw new InconsistentUserRowException(
            $"Column '{column}' is NULL but the row's status requires it.");

    private static string Required(string? value, string column) =>
        value ?? throw new InconsistentUserRowException(
            $"Column '{column}' is NULL but the row's status requires it.");
}

public sealed class UnknownUserStatusException(UserStatus status, Guid id)
    : Exception($"User row {id} has unknown status '{status}'.");

public sealed class InconsistentUserRowException(string message) : Exception(message);
