using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

/// <summary>
/// Used only by the EF Core design-time tools (<c>dotnet ef migrations ...</c>).
/// <para>
/// It exists because the real <see cref="FallahFundDbContext"/> is resolved from the web host,
/// and the web host refuses to start without a configured connection string — so without this,
/// generating a migration would demand a live database to point at. Here the connection string
/// is a placeholder: no connection is opened when a migration is scaffolded, because EF only
/// needs the provider to translate the model to DDL. <c>dotnet ef database update</c> is the
/// command that actually needs a real one.
/// </para>
/// <para>
/// This class is never referenced by application code and is excluded from the runtime graph by
/// the EF Design package's own <c>PrivateAssets</c>.
/// </para>
/// </summary>
public class FallahFundDbContextFactory : IDesignTimeDbContextFactory<FallahFundDbContext>
{
    public FallahFundDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FallahFundDbContext>()
            .UseSqlServer(
                "Server=(design-time-placeholder);Database=FallahFund;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new FallahFundDbContext(options);
    }
}
