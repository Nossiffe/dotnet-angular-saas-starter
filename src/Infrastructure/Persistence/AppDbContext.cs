using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Identity;
using Saas.Domain.Products;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenant)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> Memberships => Set<OrganizationMembership>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await Database.BeginTransactionAsync(cancellationToken);
            await operation(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });
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
