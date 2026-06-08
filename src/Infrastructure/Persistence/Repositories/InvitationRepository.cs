using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(AppDbContext db) : IInvitationRepository
{
    public Task<Invitation?> GetByIdAsync(InvitationId id, CancellationToken cancellationToken) =>
        db.Invitations.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

    public Task<bool> HasPendingInvitationAsync(OrganizationId organizationId, string email, CancellationToken cancellationToken) =>
        db.Invitations.AnyAsync(
            i => i.OrganizationId == organizationId
                && i.InviteeEmail == email
                && i.Status == InvitationStatus.Pending,
            cancellationToken);

    public void Add(Invitation invitation) => db.Invitations.Add(invitation);
}
