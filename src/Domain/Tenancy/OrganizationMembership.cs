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
    public OrganizationRole Role { get; private set; }

    internal static OrganizationMembership Create(OrganizationId organizationId, Guid userId, OrganizationRole role) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
        };

    internal void UpdateRole(OrganizationRole role) => Role = role;
}
