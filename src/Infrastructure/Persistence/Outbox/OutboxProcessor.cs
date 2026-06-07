using System.Text.Json;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Saas.Infrastructure.Persistence.Outbox;

// BackgroundService qui dispatche de façon fiable les domain events persistés dans la table outbox.
// Poll périodique → désérialise (Type + Content) → IPublisher.Publish sur le type concret → marque
// ProcessedOn. Retry borné via Attempts (< MaxAttempts). Le ProductCreatedHandler (etc.) s'exécute
// donc désormais de façon ASYNCHRONE via ce processor, plus de manière synchrone au SaveChanges.
internal sealed partial class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger)
    : BackgroundService
{
    private const int MaxAttempts = 5;
    private const int BatchSize = 20;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // un BackgroundService ne doit jamais crasher sur un batch raté
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogBatchFailed(logger, ex);
            }
        }
    }

    // Exposé pour les tests d'intégration (déclenche un batch sans attendre un tick).
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedOn == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var processed = 0;
        foreach (var message in messages)
        {
            try
            {
                var type = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"Type introuvable : {message.Type}");
                var domainEvent = JsonSerializer.Deserialize(message.Content, type, OutboxJson.Options)
                    ?? throw new InvalidOperationException($"Désérialisation nulle pour {message.Type}");

                // Cast object → dispatch sur le TYPE concret (handler généré par Mediator).
                await publisher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
                message.ProcessedOn = DateTimeOffset.UtcNow;
                processed++;
            }
#pragma warning disable CA1031 // on isole l'échec d'un message (retry borné) sans casser le batch
            catch (Exception ex)
#pragma warning restore CA1031
            {
                message.Attempts++;
                message.Error = ex.Message;
                LogMessageFailed(logger, message.Id, message.Attempts, ex);
            }
        }

        if (messages.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return processed;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox : échec du batch")]
    private static partial void LogBatchFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox : échec du message {MessageId} (tentative {Attempts})")]
    private static partial void LogMessageFailed(ILogger logger, Guid messageId, int attempts, Exception ex);
}
