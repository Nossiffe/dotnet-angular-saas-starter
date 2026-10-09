using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Persistence;

namespace Saas.Infrastructure.Jobs;

// Job Hangfire quotidien : supprime les messages de l'outbox traités depuis plus de 7 jours, avec
// leurs lignes OutboxMessageConsumers (suppression en cascade). Les messages abandonnés restent en
// base, pour être examinés puis relancés ou supprimés à la main (DEPLOYMENT.md).
internal sealed partial class PurgeProcessedOutboxMessagesJob(
    AppDbContext db,
    IDateTimeProvider clock,
    ILogger<PurgeProcessedOutboxMessagesJob> logger)
{
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    public async Task RunAsync()
    {
        var cutoff = clock.UtcNow - Retention;
        var deleted = await db.OutboxMessages
            .Where(m => m.ProcessedOn < cutoff)
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);
        LogPurged(logger, deleted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purge de l'outbox : {Count} message(s) supprimé(s)")]
    private static partial void LogPurged(ILogger logger, int count);
}
