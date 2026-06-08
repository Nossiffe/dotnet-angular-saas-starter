using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Jobs;

namespace Saas.IntegrationTests.Invitations;

/// <summary>
/// Remplaçant de test de IInvitationEmailEnqueuer : exécute ProcessInvitationEmailJob
/// SYNCHRONEMENT dans le même scope DI, de sorte que CapturingEmailSender capture
/// le mail d'invitation avant que le test n'aille lire factory.Emails.For(email).
/// </summary>
internal sealed class SynchronousInvitationEmailEnqueuer(IServiceProvider sp) : IInvitationEmailEnqueuer
{
    public void Enqueue(Guid invitationId, string rawToken, string inviteeEmail)
    {
        using var scope = sp.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<ProcessInvitationEmailJob>();
        // Exécution synchrone intentionnelle (contexte de test — pas de deadlock possible).
        job.RunAsync(invitationId, rawToken, inviteeEmail).GetAwaiter().GetResult();
    }
}
