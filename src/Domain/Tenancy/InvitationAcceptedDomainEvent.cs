using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed record InvitationAcceptedDomainEvent(
    InvitationId InvitationId,
    OrganizationId OrganizationId,
    string InviteeEmail,
    OrganizationRole Role) : IDomainEvent;
