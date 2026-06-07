using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Identity;
using Saas.Domain.Products;
using Saas.Domain.Tenancy;
using Saas.Infrastructure.Persistence.Converters;

namespace Saas.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenant)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // DbSets internes : seuls les repositories (Infrastructure) y accèdent côté write.
    // Le read passe par Dapper. L'Application ne voit plus de DbSet (pas d'IAppDbContext).
    internal DbSet<Product> Products => Set<Product>();
    internal DbSet<Organization> Organizations => Set<Organization>();
    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(cancellationToken);
            await operation(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<ProductId>().HaveConversion<ProductIdConverter>();
        configurationBuilder.Properties<OrganizationId>().HaveConversion<OrganizationIdConverter>();
        configurationBuilder.Properties<Slug>().HaveConversion<SlugConverter>().HaveMaxLength(100);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder); // REQUIS en premier : mappe les tables Identity
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Seul Product est filtré par tenant (+ soft-delete). Identity/Org/Membership = cross-tenant.
        builder.Entity<Product>().HasQueryFilter(
            p => p.TenantId == tenant.TenantId && !p.IsDeleted);
    }
}
