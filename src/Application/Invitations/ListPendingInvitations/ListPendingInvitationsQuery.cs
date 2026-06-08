using Mediator;

namespace Saas.Application.Invitations.ListPendingInvitations;

public sealed record ListPendingInvitationsQuery(Guid OrganizationId) : IQuery<IReadOnlyList<InvitationDto>>;
