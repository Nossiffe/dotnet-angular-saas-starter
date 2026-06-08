using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed record MemberJoinedDomainEvent(
    OrganizationId OrganizationId,
    Guid UserId,
    OrganizationRole Role) : IDomainEvent;
