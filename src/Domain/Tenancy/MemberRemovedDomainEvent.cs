using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed record MemberRemovedDomainEvent(
    OrganizationId OrganizationId,
    Guid UserId,
    OrganizationRole Role) : IDomainEvent;
