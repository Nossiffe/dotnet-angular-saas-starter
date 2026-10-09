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
// - Un message, une transaction : le message est verrouillé (FOR UPDATE SKIP LOCKED), ses handlers
//   sont appelés, puis son issue est validée. Une autre instance saute les messages verrouillés.
//   Un échec ou un arrêt de l'application ne défait pas les messages déjà validés.
// - Chaque handler s'exécute dans son propre scope : il a son propre DbContext et valide ses
//   propres écritures, hors de la transaction du message.
// - Échec d'un handler : le message est repris 1, 4, 16 puis 64 minutes plus tard. Les handlers
//   déjà passés sont notés dans OutboxMessageConsumers et ne sont pas rappelés. Après 5 tentatives,
//   le message est abandonné et journalisé en erreur ; DEPLOYMENT.md explique comment le relancer.
// - Livraison « au moins une fois » : un arrêt brutal entre un e-mail envoyé et le commit fait
//   rappeler ce handler. Un handler qui agit hors de la base doit le tolérer.
internal sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IDateTimeProvider clock,
    ILogger<OutboxProcessor> logger)
{
    internal const int MaxAttempts = 5;

    // Messages encore à traiter. La borne est écrite dans le SQL, pas passée en paramètre : avec un
    // plan générique (requête préparée), PostgreSQL ne pourrait plus prouver "Attempts" < 5 et
    // n'utiliserait pas l'index partiel IX_OutboxMessages_Pending, qui porte le même filtre.
    internal const string PendingFilter = "\"ProcessedOn\" IS NULL AND \"Attempts\" < 5";

    private const string LockNextDueSql = $$"""
        SELECT * FROM "OutboxMessages"
        WHERE {{PendingFilter}}
          AND ("NextAttemptOn" IS NULL OR "NextAttemptOn" <= {0})
        ORDER BY "OccurredOn", "Id"
        LIMIT 1
        FOR UPDATE SKIP LOCKED
        """;

    private const int BatchSize = 20;
    private static readonly TimeSpan _firstRetryDelay = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<Type, IHandlerInvoker> _invokers = new();

    /// <summary>Traite jusqu'à 20 messages, un par transaction.</summary>
    /// <returns>Le nombre de messages pris, traités ou en échec.</returns>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var taken = 0;
        while (taken < BatchSize && await ProcessNextAsync(cancellationToken).ConfigureAwait(false))
        {
            taken++;
        }

        return taken;
    }

    internal static TimeSpan RetryDelay(int attempts) => _firstRetryDelay * Math.Pow(4, attempts - 1);

    private async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (Guid Id, int Attempts)? locked = null;
        try
        {
            // La stratégie de retry EF exige d'envelopper une transaction explicite.
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(
                async ct =>
                {
                    db.ChangeTracker.Clear();
                    await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
                    var message = await LockNextDueAsync(db, ct).ConfigureAwait(false);
                    if (message is null)
                    {
                        return false;
                    }

                    locked = (message.Id, message.Attempts);
                    await DispatchAsync(db, scope.ServiceProvider, message, ct).ConfigureAwait(false);
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // l'issue n'a pas pu être enregistrée : la tentative est comptée à part
        catch (Exception ex) when (locked is not null && !cancellationToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            await RecordUnsavedAttemptAsync(locked.Value.Id, locked.Value.Attempts + 1, ex, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
    }

    private async Task<OutboxMessage?> LockNextDueAsync(AppDbContext db, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var due = await db.OutboxMessages
            .FromSqlRaw(LockNextDueSql, now)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return due.SingleOrDefault();
    }

    private async Task DispatchAsync(
        AppDbContext db, IServiceProvider services, OutboxMessage message, CancellationToken ct)
    {
        try
        {
            var domainEvent = Deserialize(message);
            var invoker = _invokers.GetOrAdd(
                domainEvent.GetType(),
                type => (IHandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(type))!);
            var alreadyHandled = await db.OutboxMessageConsumers
                .Where(c => c.MessageId == message.Id)
                .Select(c => c.Handler)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var handlerType in invoker.HandlerTypes(services))
            {
                var name = handlerType.FullName!;
                if (alreadyHandled.Contains(name, StringComparer.Ordinal))
                {
                    continue;
                }

                await using (var handlerScope = scopeFactory.CreateAsyncScope())
                {
                    await invoker.InvokeAsync(handlerScope.ServiceProvider, handlerType, domainEvent, ct)
                        .ConfigureAwait(false);
                }

                db.OutboxMessageConsumers.Add(new OutboxMessageConsumer { MessageId = message.Id, Handler = name });
            }

            message.ProcessedOn = clock.UtcNow;
            message.NextAttemptOn = null;
            message.Error = null;
        }
#pragma warning disable CA1031 // l'échec d'un handler est noté sur le message, qui sera repris
        catch (Exception ex) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
        {
            message.Attempts++;
            message.NextAttemptOn = NextAttemptOn(message.Attempts);
            message.Error = ex.Message.Replace('\0', ' ');
            LogFailure(message.Id, message.Attempts, message.NextAttemptOn, ex);
        }
    }

    // L'issue du message n'a pas été validée (commit refusé par PostgreSQL, par exemple). Sans
    // tentative comptée, le même message serait repris à chaque lot, indéfiniment.
    private async Task RecordUnsavedAttemptAsync(Guid messageId, int attempts, Exception ex, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var nextAttemptOn = NextAttemptOn(attempts);
        await db.OutboxMessages
            .Where(m => m.Id == messageId && m.ProcessedOn == null)
            .ExecuteUpdateAsync(
                m => m
                    .SetProperty(x => x.Attempts, attempts)
                    .SetProperty(x => x.NextAttemptOn, nextAttemptOn)
                    .SetProperty(x => x.Error, "L'issue du message n'a pas pu être enregistrée (voir les logs)."),
                ct)
            .ConfigureAwait(false);
        LogFailure(messageId, attempts, nextAttemptOn, ex);
    }

    // Null après la dernière tentative : le message est abandonné.
    private DateTimeOffset? NextAttemptOn(int attempts)
        => attempts >= MaxAttempts ? null : clock.UtcNow + RetryDelay(attempts);

    private void LogFailure(Guid messageId, int attempts, DateTimeOffset? nextAttemptOn, Exception ex)
    {
        if (nextAttemptOn is { } next)
        {
            LogMessageFailed(logger, messageId, attempts, next, ex);
        }
        else
        {
            LogMessageAbandoned(logger, messageId, attempts, ex);
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

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Outbox : échec du message {MessageId} (tentative {Attempts}), repris à partir de {NextAttemptOn}")]
    private static partial void LogMessageFailed(
        ILogger logger, Guid messageId, int attempts, DateTimeOffset nextAttemptOn, Exception ex);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Outbox : message {MessageId} abandonné après {Attempts} tentatives")]
    private static partial void LogMessageAbandoned(ILogger logger, Guid messageId, int attempts, Exception ex);

    // Appelle les handlers d'un type d'event sans réflexion à chaque message : une instance par type.
    private interface IHandlerInvoker
    {
        IReadOnlyList<Type> HandlerTypes(IServiceProvider services);

        ValueTask InvokeAsync(IServiceProvider services, Type handlerType, IDomainEvent domainEvent, CancellationToken ct);
    }

    private sealed class HandlerInvoker<TEvent> : IHandlerInvoker
        where TEvent : IDomainEvent
    {
        public IReadOnlyList<Type> HandlerTypes(IServiceProvider services)
            => [.. services.GetServices<IDomainEventHandler<TEvent>>().Select(handler => handler.GetType())];

        public ValueTask InvokeAsync(
            IServiceProvider services, Type handlerType, IDomainEvent domainEvent, CancellationToken ct)
            => services.GetServices<IDomainEventHandler<TEvent>>()
                .First(handler => handler.GetType() == handlerType)
                .HandleAsync((TEvent)domainEvent, ct);
    }
}
