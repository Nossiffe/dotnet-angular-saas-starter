using Hangfire;
using Saas.Application.Common.Interfaces;

namespace Saas.Infrastructure.Jobs;

/// <summary>
/// Implémentation de IInvitationEmailEnqueuer via Hangfire.
/// Enfile le job ProcessInvitationEmailJob en arrière-plan — la réponse HTTP peut retourner
/// immédiatement sans attendre l'envoi SMTP.
/// </summary>
internal sealed class HangfireInvitationEmailEnqueuer(IBackgroundJobClient jobs) : IInvitationEmailEnqueuer
{
    public void Enqueue(Guid invitationId, string rawToken, string inviteeEmail)
        => jobs.Enqueue<ProcessInvitationEmailJob>(j => j.RunAsync(invitationId, rawToken, inviteeEmail));
}
