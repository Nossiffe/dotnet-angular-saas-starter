using ErrorOr;
using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

// L'organisation EST le tenant. Non multi-tenant elle-même (cross-tenant).
public sealed class Organization : AggregateRoot<OrganizationId>, IAuditable
{
    private readonly List<OrganizationMembership> _memberships = [];

    private Organization()
    {
        // EF
    }

    public string Name { get; private set; } = null!;
    public Slug Slug { get; private set; }

    public IReadOnlyList<OrganizationMembership> Memberships => _memberships.AsReadOnly();

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public static Organization Create(string name, Slug slug, Guid ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nom requis.", nameof(name));
        }

        var organization = new Organization
        {
            Id = OrganizationId.From(Guid.CreateVersion7()),
            Name = name.Trim(),
            Slug = slug,
        };
        organization.AddMember(ownerUserId, OrganizationRole.Owner); // intentionnel : émet MemberJoinedDomainEvent pour le Owner à la création
        return organization;
    }

    public void AddMember(Guid userId, OrganizationRole role)
    {
        if (_memberships.Exists(m => m.UserId == userId))
        {
            return;
        }

        _memberships.Add(OrganizationMembership.Create(Id, userId, role));
        Raise(new MemberJoinedDomainEvent(Id, userId, role));
    }

    public ErrorOr<Success> ChangeMemberRole(Guid userId, OrganizationRole newRole)
    {
        var membership = _memberships.Find(m => m.UserId == userId);
        if (membership is null)
        {
            return OrganizationErrors.MemberNotFound;
        }

        var oldRole = membership.Role;
        if (oldRole == newRole)
        {
            return Result.Success;
        }

        // Garde du dernier Owner : ne jamais rétrograder le seul Owner (laisserait 0 Owner).
        if (oldRole == OrganizationRole.Owner
            && _memberships.Count(m => m.Role == OrganizationRole.Owner) == 1)
        {
            return OrganizationErrors.LastOwner;
        }

        membership.UpdateRole(newRole);
        Raise(new MemberRoleChangedDomainEvent(Id, userId, oldRole, newRole));
        return Result.Success;
    }
}
