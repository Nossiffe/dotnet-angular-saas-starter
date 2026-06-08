namespace Saas.Application.Common.Interfaces;

/// <summary>Enfile l'envoi de l'email d'invitation vers un job d'arrière-plan (Hangfire).</summary>
public interface IInvitationEmailEnqueuer
{
    void Enqueue(Guid invitationId, string rawToken, string inviteeEmail);
}
