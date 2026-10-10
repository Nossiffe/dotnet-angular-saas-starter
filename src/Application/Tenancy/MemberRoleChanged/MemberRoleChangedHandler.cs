using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberRoleChanged;

public sealed partial class MemberRoleChangedHandler(ILogger<MemberRoleChangedHandler> logger)
    : IDomainEventHandler<MemberRoleChangedDomainEvent>
{
    public ValueTask HandleAsync(MemberRoleChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        LogMemberRoleChanged(logger, domainEvent.UserId, domainEvent.OrganizationId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rôle de membre changé : utilisateur {UserId} sur l'organisation {OrganizationId}")]
    private static partial void LogMemberRoleChanged(ILogger logger, Guid userId, Guid organizationId);
}
