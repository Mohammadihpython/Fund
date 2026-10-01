using Domain.Users;
using Infrastructure.Mapping;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Adapts <see cref="IUserRepository"/> to EF Core. Every decision about *what* is legal has
/// already been made by the domain; this class only moves bytes and translates shapes. There is
/// no status switch and no rule here — that is the sign the port is doing its job.
/// </summary>
public sealed class EfUserRepository(FallahFundDbContext db) : IUserRepository
{
    public async Task<UserState?> FindByIdAsync(UserId id, CancellationToken ct = default) =>
        await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id.Value, ct) is { } row
            ? UserMapper.FromPersistence(row)
            : null;

    public async Task<UserState?> FindByEmailAsync(Email email, CancellationToken ct = default) =>
        await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email.Value, ct) is { } row
            ? UserMapper.FromPersistence(row)
            : null;

    public Task<bool> EmailExistsAsync(Email email, CancellationToken ct = default) =>
        db.Users.AnyAsync(u => u.Email == email.Value, ct);

    public async Task SaveAsync(UserState user, CancellationToken ct = default)
    {
        var existing = await db.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id.Value, ct);

        if (existing is null)
        {
            existing = new UserEntity
            {
                Id = user.Id.Value,
                Email = user.Email.Value,
                PasswordHash = user.Password.Value,
                RolesJson = "[]",
                RegisteredAt = user.RegisteredAt
            };
            db.Users.Add(existing);
        }

        UserMapper.ApplyToEntity(user, existing);
        await db.SaveChangesAsync(ct);
    }
}
