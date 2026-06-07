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
        organization.AddMember(ownerUserId, "Owner");
        return organization;
    }

    public void AddMember(Guid userId, string role)
    {
        if (_memberships.Exists(m => m.UserId == userId))
        {
            return;
        }

        _memberships.Add(OrganizationMembership.Create(Id, userId, role));
    }
}
