using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;

namespace Saas.Infrastructure.Persistence.Interceptors;

// Renseigne le tenant, l'audit et la suppression logique. Ces propriétés n'ont pas de setter
// public dans le Domain : seules les écritures EF ci-dessous les modifient.
public sealed class AuditableEntityInterceptor(
    ICurrentUser currentUser,
    ITenantProvider tenant,
    IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Apply(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext context)
    {
        var now = clock.UtcNow;
        var user = currentUser.UserId;

        foreach (EntityEntry<ITenantOwned> entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            var tenantId = entry.Property(nameof(ITenantOwned.TenantId));
            if (entry.State is EntityState.Added)
            {
                tenantId.CurrentValue = tenant.TenantId;
            }
            else if (tenantId.IsModified)
            {
                throw new InvalidOperationException(
                    $"Le tenant d'une entité {entry.Metadata.ClrType.Name} existante ne peut pas changer.");
            }
        }

        // ISoftDeletable must run before IAuditable so that soft-deleted entities
        // are promoted to Modified before IAuditable sets UpdatedAt/UpdatedBy on them.
        foreach (EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State is EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;
            entry.Property(nameof(ISoftDeletable.DeletedAt)).CurrentValue = now;

            // EF a marqué les owned dependents (ex. Money/Price) en Deleted en cascade.
            // On les remet en Unchanged pour ne pas supprimer physiquement les colonnes owned.
            foreach (var reference in entry.References)
            {
                if (reference.TargetEntry is { State: EntityState.Deleted } owned
                    && owned.Metadata.IsOwned())
                {
                    owned.State = EntityState.Unchanged;
                }
            }
        }

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = user;
            }
            else if (entry.State is EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = now;
                entry.Property(nameof(IAuditable.UpdatedBy)).CurrentValue = user;
            }
        }
    }
}
