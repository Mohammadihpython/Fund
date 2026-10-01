using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

/// <summary>
/// Composition root for the database. The only class that knows which provider is in use.
/// </summary>
public class FallahFundDbContext(DbContextOptions<FallahFundDbContext> options) : DbContext(options)
{
    public DbSet<Persistence.Entities.UserEntity> Users => Set<Persistence.Entities.UserEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new Configuration.UserConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
