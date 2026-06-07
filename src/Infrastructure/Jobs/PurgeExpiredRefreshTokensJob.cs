using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Persistence;

namespace Saas.Infrastructure.Jobs;

// Job Hangfire RÉEL : purge quotidienne des refresh tokens expirés ou révoqués.
// Set-based via ExecuteDeleteAsync (pas de tracking). Enregistré comme recurring (Cron.Daily)
// dans Program.cs ; peut aussi être enfilé à la demande : BackgroundJob.Enqueue<PurgeExpiredRefreshTokensJob>(j => j.RunAsync()).
internal sealed partial class PurgeExpiredRefreshTokensJob(
    AppDbContext db,
    IDateTimeProvider clock,
    ILogger<PurgeExpiredRefreshTokensJob> logger)
{
    public async Task RunAsync()
    {
        var cutoff = clock.UtcNow;
        var deleted = await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff || t.RevokedAt != null)
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);
        LogPurged(logger, deleted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purge refresh tokens : {Count} supprimé(s)")]
    private static partial void LogPurged(ILogger logger, int count);
}
