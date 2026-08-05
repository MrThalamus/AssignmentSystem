using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AssignmentSystem.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> at design time. Without it the tooling would have to boot
/// the API host, which refuses to start unless a JWT signing key is configured -
/// irrelevant for generating migrations, and awkward for anyone scripting them.
/// The connection string only has to be valid enough to pick the provider; migrations
/// are generated without contacting the server.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    private const string FallbackConnectionString =
        "Host=localhost;Port=5432;Database=assignment_system;Username=postgres;Password=postgres";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? FallbackConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
