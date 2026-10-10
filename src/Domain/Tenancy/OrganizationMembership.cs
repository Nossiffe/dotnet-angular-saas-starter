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

    /// <summary>Date d'arrivée dans l'organisation : sa création pour l'Owner, l'acceptation pour un invité.</summary>
    public DateTimeOffset JoinedAt { get; private set; }

    internal static OrganizationMembership Create(
        OrganizationId organizationId, Guid userId, OrganizationRole role, DateTimeOffset joinedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            JoinedAt = joinedAt,
        };

    internal void UpdateRole(OrganizationRole role) => Role = role;
}
