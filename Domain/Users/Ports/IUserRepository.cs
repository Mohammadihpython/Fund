namespace Domain.Users;

/// <summary>
/// Persistence port for the user aggregate. The state hierarchy is what comes back out —
/// an adapter is responsible for turning a flat database row (one status column, several
/// nullable columns) back into the right sealed record, so no caller ever sees the flat
/// shape or writes a status switch of its own.
/// </summary>
public interface IUserRepository
{
    Task<UserState?> FindByIdAsync(UserId id, CancellationToken ct = default);

    /// <summary>Email is unique, so this is the lookup behind registration and sign-in.</summary>
    Task<UserState?> FindByEmailAsync(Email email, CancellationToken ct = default);

    Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default);

    Task SaveAsync(UserState user, CancellationToken ct = default);
}
