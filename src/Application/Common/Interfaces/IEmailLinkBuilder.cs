namespace Saas.Application.Common.Interfaces;

/// <summary>Construit les liens front (base URL en config) pointés dans les emails transactionnels.</summary>
public interface IEmailLinkBuilder
{
    string ConfirmEmailLink(Guid userId, string token);
    string ResetPasswordLink(Guid userId, string token);
    string AcceptInvitationLink(Guid invitationId, string token);
}
