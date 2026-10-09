using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Saas.Infrastructure.Persistence.Outbox;

// Lance un lot de l'outbox toutes les 5 secondes. À l'arrêt de l'application, le message en cours
// est abandonné sans être compté comme un échec ; il sera repris au prochain démarrage.
internal sealed partial class OutboxPollingService(OutboxProcessor processor, ILogger<OutboxPollingService> logger)
    : BackgroundService
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunBatchAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Arrêt de l'application : rien à journaliser.
        }
    }

    private async Task RunBatchAsync(CancellationToken stoppingToken)
    {
        try
        {
            await processor.ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // un lot raté (base indisponible) ne doit pas arrêter le service
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            LogBatchFailed(logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox : échec du lot")]
    private static partial void LogBatchFailed(ILogger logger, Exception ex);
}
