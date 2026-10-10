using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed record InvitationResentDomainEvent(
    InvitationId InvitationId,
    OrganizationId OrganizationId) : IDomainEvent;
