using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberRemoved;

// Révoque les refresh tokens du membre retiré sur cette organisation. Le refresh vérifie déjà
// l'adhésion : dès le retrait, il refuse le token et révoque alors les sessions du membre sur
// l'organisation. Mais si aucun appareil du membre ne tente de refresh entre le retrait et une
// nouvelle invitation, ses refresh tokens restent actifs ; une fois membre de nouveau, il
// retrouverait ses anciennes sessions tant qu'elles n'ont pas expiré (RefreshTokenDays). Révoquer
// est idempotent : un nouvel essai de l'outbox ne change rien. Un membre qui aurait rejoint de
// nouveau avant le passage de l'outbox perdrait aussi ses nouvelles sessions : il se reconnecte.
public sealed partial class MemberRemovedHandler(
    IRefreshTokenStore refreshTokens,
    ILogger<MemberRemovedHandler> logger)
    : IDomainEventHandler<MemberRemovedDomainEvent>
{
    public async ValueTask HandleAsync(MemberRemovedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        await refreshTokens.RevokeTenantSessionsAsync(
            domainEvent.UserId, domainEvent.OrganizationId.Value, cancellationToken);
        LogMemberRemoved(logger, domainEvent.UserId, domainEvent.OrganizationId.Value);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Membre retiré : utilisateur {UserId} de l'organisation {OrganizationId}, sessions révoquées")]
    private static partial void LogMemberRemoved(ILogger logger, Guid userId, Guid organizationId);
}
