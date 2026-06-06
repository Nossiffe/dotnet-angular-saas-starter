using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Saas.Infrastructure.Identity;

namespace Saas.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=saas;Username=saas;Password=saas";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new StubTenantProvider());
    }
}
