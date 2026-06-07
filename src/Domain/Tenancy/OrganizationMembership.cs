using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed class OrganizationMembership : BaseEntity
{
    public required Guid OrganizationId { get; set; }
    public required Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
}
