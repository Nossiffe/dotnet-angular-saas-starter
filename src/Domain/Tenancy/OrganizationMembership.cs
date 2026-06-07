using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

public sealed class OrganizationMembership : Entity<Guid>
{
    private OrganizationMembership()
    {
        // EF
    }

    public OrganizationId OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = "Member";

    internal static OrganizationMembership Create(OrganizationId organizationId, Guid userId, string role) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
        };
}
