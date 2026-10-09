using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Infrastructure.Persistence.Outbox;

namespace Saas.IntegrationTests.Outbox;

/// <summary>
/// Refuse d'enregistrer l'issue d'une sonde <c>FailsToSave</c>, comme PostgreSQL refuserait un
/// commit : le processeur doit quand même compter la tentative.
/// </summary>
public sealed class UnsavableProbeInterceptor : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        var refused = eventData.Context?.ChangeTracker.Entries<OutboxMessage>()
            .Any(e => e.State == EntityState.Modified
                && e.Entity.Content.Contains("\"failsToSave\":true", StringComparison.Ordinal)) ?? false;
        return refused
            ? throw new DbUpdateException("Enregistrement refusé (simulé).")
            : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
