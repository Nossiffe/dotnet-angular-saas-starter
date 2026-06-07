using Hangfire;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Jobs;

/// <summary>
/// Implémentation de IPasswordResetEnqueuer via Hangfire.
/// Enfile le job ProcessPasswordResetJob en arrière-plan — la réponse HTTP peut retourner
/// immédiatement sans attendre le lookup DB, la génération de token ou l'envoi SMTP.
/// </summary>
internal sealed class PasswordResetRequestQueue(IBackgroundJobClient jobs) : IPasswordResetEnqueuer
{
    public void Enqueue(string email)
        => jobs.Enqueue<ProcessPasswordResetJob>(j => j.RunAsync(email));
}
