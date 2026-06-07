using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Repositories;

public sealed class OrganizationRepository(AppDbContext db) : IOrganizationRepository
{
    public Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken cancellationToken) =>
        db.Organizations
            .Include(o => o.Memberships)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Organization organization) => db.Organizations.Add(organization);
}
