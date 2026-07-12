using ErrorOr;
using Mediator;

namespace Saas.Application.Invitations.RevokeInvitation;

public sealed record RevokeInvitationCommand(Guid OrganizationId, Guid InvitationId) : ICommand<ErrorOr<Success>>;
