using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed record MemberInvitedDomainEvent(
    InvitationId InvitationId,
    OrganizationId OrganizationId,
    string InviteeEmail) : IDomainEvent;
