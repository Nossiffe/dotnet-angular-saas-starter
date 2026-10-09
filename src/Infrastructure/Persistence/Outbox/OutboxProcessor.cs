using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;

namespace Saas.Infrastructure.Persistence.Outbox;

// Transmet les domain events de l'outbox à leurs IDomainEventHandler. OutboxPollingService lance
// un lot toutes les 5 secondes ; les tests d'intégration lancent les lots eux-mêmes.
//
// - Plusieurs instances : chaque lot est lu avec FOR UPDATE SKIP LOCKED, dans une transaction. Une
//   instance ne lit pas les lignes qu'une autre est en train de traiter.
// - Échec d'un handler : le message est retenté (5 tentatives au plus). Les handlers déjà passés
//   sont notés dans OutboxMessageConsumers et ne sont pas rappelés.
// - Livraison « au moins une fois » : un arrêt brutal entre un e-mail envoyé et le commit fait
//   rappeler ce handler. Un handler qui agit hors de la base doit le tolérer.
internal sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider clock,
    ILogger<OutboxProcessor> logger)
{
    private const int MaxAttempts = 5;
    private const int BatchSize = 20;
    private static readonly ConcurrentDictionary<Type, IHandlerInvoker> _invokers = new();

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // La stratégie de retry EF exige d'envelopper une transaction explicite.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(
            async ct =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

                var messages = await db.OutboxMessages
                    .FromSql($"""
                        SELECT * FROM "OutboxMessages"
                        WHERE "ProcessedOn" IS NULL AND "Attempts" < {MaxAttempts}
                        ORDER BY "OccurredOn", "Id"
                        LIMIT {BatchSize}
                        FOR UPDATE SKIP LOCKED
                        """)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var processed = 0;
                foreach (var message in messages)
                {
                    if (await DispatchAsync(db, scope.ServiceProvider, message, ct).ConfigureAwait(false))
                    {
                        processed++;
                    }
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return processed;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> DispatchAsync(
        AppDbContext db, IServiceProvider services, OutboxMessage message, CancellationToken ct)
    {
        try
        {
            var domainEvent = Deserialize(message);
            var alreadyHandled = await db.OutboxMessageConsumers
                .Where(c => c.MessageId == message.Id)
                .Select(c => c.Handler)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var invoker = _invokers.GetOrAdd(
                domainEvent.GetType(),
                type => (IHandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(type))!);

            foreach (var (name, handle) in invoker.Handlers(services, domainEvent))
            {
                if (alreadyHandled.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }

                await handle(ct).ConfigureAwait(false);
                db.OutboxMessageConsumers.Add(new OutboxMessageConsumer { MessageId = message.Id, Handler = name });
            }

            message.ProcessedOn = clock.UtcNow;
            message.Error = null;
            return true;
        }
#pragma warning disable CA1031 // l'échec d'un message est noté (retry borné) sans casser le lot
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            message.Attempts++;
            message.Error = ex.Message;
            LogMessageFailed(logger, message.Id, message.Attempts, ex);
            return false;
        }
    }

    // Seuls des domain events sont désérialisés, jamais un type arbitraire lu en base.
    private static IDomainEvent Deserialize(OutboxMessage message)
    {
        var type = Type.GetType(message.Type);
        if (type is null || !typeof(IDomainEvent).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Type de domain event inconnu : {message.Type}");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(message.Content, type, OutboxJson.Options)
            ?? throw new InvalidOperationException($"Désérialisation nulle pour {message.Type}"));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox : échec du message {MessageId} (tentative {Attempts})")]
    private static partial void LogMessageFailed(ILogger logger, Guid messageId, int attempts, Exception ex);

    // Appelle les handlers d'un type d'event sans réflexion à chaque message : une instance par type.
    private interface IHandlerInvoker
    {
        IEnumerable<(string Name, Func<CancellationToken, ValueTask> Handle)> Handlers(
            IServiceProvider services, IDomainEvent domainEvent);
    }

    private sealed class HandlerInvoker<TEvent> : IHandlerInvoker
        where TEvent : IDomainEvent
    {
        public IEnumerable<(string Name, Func<CancellationToken, ValueTask> Handle)> Handlers(
            IServiceProvider services, IDomainEvent domainEvent)
            => services.GetServices<IDomainEventHandler<TEvent>>()
                .Select(handler => (
                    handler.GetType().FullName!,
                    (Func<CancellationToken, ValueTask>)(ct => handler.HandleAsync((TEvent)domainEvent, ct))));
    }
}
