using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Persistence;

namespace Saas.Infrastructure.Jobs;

// Job Hangfire : purge quotidienne des refresh tokens expirés.
// Set-based via ExecuteDeleteAsync (pas de tracking). Enregistré comme job récurrent (Cron.Daily)
// par DependencyInjection.RegisterRecurringJobs.
//
// On ne purge que les tokens dont ExpiresAt < now (expirés), même s'ils sont révoqués.
// Les tokens remplacés par une rotation mais encore dans leur fenêtre de vie sont conservés :
// présentés de nouveau, RefreshTokenStore.ValidateAndRotateAsync y voit un signal de vol et
// révoque toutes les sessions de l'utilisateur. Purgé trop tôt, un tel token serait traité comme
// "non trouvé" (401 ordinaire) au lieu de déclencher cette révocation. Fenêtre de rétention = durée de vie du refresh
// (RefreshTokenDays, 7 jours par défaut).
internal sealed partial class PurgeExpiredRefreshTokensJob(
    AppDbContext db,
    IDateTimeProvider clock,
    ILogger<PurgeExpiredRefreshTokensJob> logger)
{
    public async Task RunAsync()
    {
        var cutoff = clock.UtcNow;
        var deleted = await db.RefreshTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync()
            .ConfigureAwait(false);
        LogPurged(logger, deleted);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Purge refresh tokens : {Count} supprimé(s)")]
    private static partial void LogPurged(ILogger logger, int count);
}
