using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Infrastructure.Persistence.Outbox;

namespace Saas.Infrastructure.Persistence.Interceptors;

// TRANSACTIONAL OUTBOX — remplace le dispatch direct (post-save) par une écriture intra-transaction.
//
// Pendant SavingChanges(Async), on collecte les domain events des agrégats trackés et on les
// sérialise en lignes OutboxMessage AJOUTÉES au même DbContext : elles participent donc à la MÊME
// transaction que l'agrégat → atomicité (aucun event perdu sur crash, aucun event publié sans commit).
// Le dispatch effectif (IPublisher.Publish) est désormais propriété de l'OutboxProcessor.
//
// SEMANTIQUE AT-LEAST-ONCE : combiné avec la stratégie de retry EF (ExecuteInTransactionAsync),
// une transaction rejouée peut produire des lignes outbox dupliquées → un même event peut être
// dispatché PLUSIEURS FOIS. Les handlers de domain events DOIVENT donc être IDEMPOTENTS.
//
// Contrainte conservée : le dispatch passe par le générateur Mediator → l'assembly qui DÉFINIT le
// type d'événement doit figurer dans options.AddMediator(... Assemblies ...).
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
