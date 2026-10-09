using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Infrastructure.Persistence.Outbox;

namespace Saas.Infrastructure.Persistence.Interceptors;

// Transactional outbox : pendant SavingChanges(Async), les domain events des agrégats suivis sont
// écrits en lignes OutboxMessage dans le même DbContext, donc dans la même transaction que l'agrégat.
// Aucun event n'est perdu sur crash, et aucun n'est publié sans commit. L'OutboxProcessor les
// transmet ensuite aux handlers.
//
// Une transaction rejouée par la stratégie de retry EF peut écrire deux fois le même event : la
// livraison est « au moins une fois », et les handlers doivent tolérer un doublon.
public sealed class DomainEventsInterceptor(IDateTimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        WriteOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        WriteOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var aggregates = context.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0)
        {
            return;
        }

        var occurredOn = clock.UtcNow;
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Type = domainEvent.GetType().AssemblyQualifiedName!,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxJson.Options),
                    OccurredOn = occurredOn,
                });
            }

            aggregate.ClearDomainEvents();
        }
    }
}
