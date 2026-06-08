using Mediator;

namespace Saas.Application.Invitations.ResendInvitation;

public sealed record ResendInvitationCommand(Guid OrganizationId, Guid InvitationId) : ICommand;
