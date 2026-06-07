using Saas.Domain.Tenancy;

namespace Saas.Application.Common.Interfaces;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(OrganizationId id, CancellationToken cancellationToken);
    void Add(Organization organization);
}
