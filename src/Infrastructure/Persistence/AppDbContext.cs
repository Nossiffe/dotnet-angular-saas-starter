using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Identity;
using Saas.Domain.Products;
using Saas.Domain.Tenancy;
using Saas.Infrastructure.Persistence.Converters;
using Saas.Infrastructure.Persistence.Outbox;

namespace Saas.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantProvider tenant)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    // DbSets internes : seuls les repositories (Infrastructure) y accèdent côté write.
    // Le read passe par Dapper. L'Application ne voit plus de DbSet (pas d'IAppDbContext).
    internal DbSet<Product> Products => Set<Product>();
    internal DbSet<Organization> Organizations => Set<Organization>();
    internal DbSet<Invitation> Invitations => Set<Invitation>();
    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
        configurationBuilder.Properties<InvitationId>().HaveConversion<InvitationIdConverter>();
        configurationBuilder.Properties<Slug>().HaveConversion<SlugConverter>().HaveMaxLength(100);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        base.OnModelCreating(builder); // REQUIS en premier : mappe les tables Identity
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Filtres de sécurité PAR CONVENTION (EF Core 10 named query filters) :
        // - "TenantFilter"     : toute entité ITenantOwned — JAMAIS désactivable métier.
        // - "SoftDeleteFilter" : toute entité ISoftDeletable — désactivable ponctuellement
        //   via IgnoreQueryFilters(["SoftDeleteFilter"]) SANS lever le filtre tenant.
        //
        // Invocation des helpers génériques privés via réflexion (MethodInfo mis en cache
        // statiquement, cf. _applyTenantFilterMethod/_applySoftDeleteFilterMethod) : seul moyen
        // d'appeler HasQueryFilter<TEntity> pour un ClrType découvert dynamiquement au sein
        // du modèle. Accès contrôlé (même classe, pas d'entrée utilisateur) — sûr.
        foreach (var clrType in builder.Model.GetEntityTypes().Select(entityType => entityType.ClrType))
        {
            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                _applyTenantFilterMethod.MakeGenericMethod(clrType).Invoke(this, [builder]);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                _applySoftDeleteFilterMethod.MakeGenericMethod(clrType).Invoke(null, [builder]);
            }
        }
    }

    // Bypass d'accessibilité volontaire (S3011) : réflexion sur des helpers PRIVÉS de
    // CETTE MÊME classe (pas d'entrée utilisateur, pas de type externe) — seul moyen
    // d'appeler HasQueryFilter<TEntity> générique pour un ClrType découvert dynamiquement
    // dans le modèle. MethodInfo mis en cache statiquement (pas de résolution par requête).
#pragma warning disable S3011
    private static readonly MethodInfo _applyTenantFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter),
            BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static readonly MethodInfo _applySoftDeleteFilterMethod =
        typeof(AppDbContext).GetMethod(nameof(ApplySoftDeleteFilter),
            BindingFlags.NonPublic | BindingFlags.Static)!;
#pragma warning restore S3011

    // Instance (non static) : la lambda référence le champ `tenant` du constructeur
    // primaire DANS l'expression — évaluation par instance/requête (cf. TenantIsolationTests).
    private void ApplyTenantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ITenantOwned
        => builder.Entity<TEntity>().HasQueryFilter("TenantFilter", e => e.TenantId == tenant.TenantId);

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, ISoftDeletable
        => builder.Entity<TEntity>().HasQueryFilter("SoftDeleteFilter", e => !e.IsDeleted);
}
