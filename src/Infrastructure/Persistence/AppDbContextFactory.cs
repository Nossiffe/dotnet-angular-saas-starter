using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private sealed class DesignTimeTenant : ITenantProvider
    {
        public Guid TenantId => Guid.Empty; // design-time : pas de tenant
    }

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=saas;Username=saas;Password=saas";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, new DesignTimeTenant());
    }
}
