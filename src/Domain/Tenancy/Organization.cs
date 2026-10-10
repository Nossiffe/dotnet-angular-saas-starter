using ErrorOr;
using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

// L'organisation est le tenant. Non multi-tenant elle-même (cross-tenant).
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

    // Audit : renseigné par l'intercepteur EF. Les setters privés n'existent que pour EF (S1144).
#pragma warning disable S1144
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
#pragma warning restore S1144

    public static Organization Create(string name, Slug slug, Guid ownerUserId, DateTimeOffset createdAt)
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
        organization.AddMember(ownerUserId, OrganizationRole.Owner, createdAt); // intentionnel : émet MemberJoinedDomainEvent pour le Owner à la création
        return organization;
    }

    public void AddMember(Guid userId, OrganizationRole role, DateTimeOffset joinedAt)
    {
        if (_memberships.Exists(m => m.UserId == userId))
        {
            return;
        }

        _memberships.Add(OrganizationMembership.Create(Id, userId, role, joinedAt));
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
        // Le décompte porte sur les adhésions chargées, sans verrou : deux requêtes simultanées
        // qui rétrogradent chacune un Owner différent passeraient toutes les deux. Aujourd'hui,
        // l'API ne crée qu'un Owner par organisation : ChangeMemberRoleValidator n'accepte que
        // Member et Admin, et une invitation ne peut pas porter le rôle Owner. Si une promotion en
        // Owner est ajoutée, il faudra protéger ce décompte contre la concurrence.
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
