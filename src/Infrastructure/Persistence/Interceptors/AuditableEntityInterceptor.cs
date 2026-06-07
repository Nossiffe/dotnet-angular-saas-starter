using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;

namespace Saas.Infrastructure.Persistence.Interceptors;

public sealed class AuditableEntityInterceptor(
    ICurrentUser currentUser,
    ITenantProvider tenant,
    IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
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

        foreach (EntityEntry<ITenantOwned> entry in context.ChangeTracker.Entries<ITenantOwned>()
            .Where(e => e.State is EntityState.Added))
        {
            entry.Entity.TenantId = tenant.TenantId;
        }

        // ISoftDeletable must run BEFORE IAuditable so that soft-deleted entities
        // are promoted to Modified before IAuditable sets UpdatedAt/UpdatedBy on them.
        foreach (EntityEntry<ISoftDeletable> entry in context.ChangeTracker.Entries<ISoftDeletable>()
            .Where(e => e.State is EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;

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
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = user;
            }
            else if (entry.State is EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = user;
            }
        }
    }
}
