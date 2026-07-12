using ErrorOr;
using Mediator;

namespace Saas.Application.Invitations.ListPendingInvitations;

public sealed record ListPendingInvitationsQuery(Guid OrganizationId)
    : IQuery<ErrorOr<IReadOnlyList<InvitationDto>>>;
