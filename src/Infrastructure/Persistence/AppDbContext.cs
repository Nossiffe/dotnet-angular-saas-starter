using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Products;

namespace Saas.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenant)
    : DbContext(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Filtre global : isolation tenant + soft-delete (Product).
        modelBuilder.Entity<Product>().HasQueryFilter(
            p => p.TenantId == tenant.TenantId && !p.IsDeleted);

        base.OnModelCreating(modelBuilder);
    }
}
