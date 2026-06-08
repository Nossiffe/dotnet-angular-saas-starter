using Saas.Domain.Tenancy;

namespace Saas.Application.Common.Interfaces;

public interface IInvitationRepository
{
    Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken);
    Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<bool> HasPendingInvitationAsync(OrganizationId organizationId, string email, CancellationToken cancellationToken);
    void Add(Invitation invitation);
}
