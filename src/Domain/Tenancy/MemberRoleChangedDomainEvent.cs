using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed record MemberRoleChangedDomainEvent(
    OrganizationId OrganizationId,
    Guid UserId,
    OrganizationRole OldRole,
    OrganizationRole NewRole) : IDomainEvent;
