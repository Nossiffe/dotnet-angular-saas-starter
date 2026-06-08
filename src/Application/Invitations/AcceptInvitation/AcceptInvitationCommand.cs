using Mediator;

namespace Saas.Application.Invitations.AcceptInvitation;

public sealed record AcceptInvitationCommand(Guid InvitationId, string Token, string? Password)
    : ICommand<AcceptInvitationResult>;
