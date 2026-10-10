using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberJoined;

public sealed partial class MemberJoinedHandler(ILogger<MemberJoinedHandler> logger)
    : IDomainEventHandler<MemberJoinedDomainEvent>
{
    public ValueTask HandleAsync(MemberJoinedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        LogMemberJoined(logger, domainEvent.UserId, domainEvent.OrganizationId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Membre rejoint : utilisateur {UserId} sur l'organisation {OrganizationId}")]
    private static partial void LogMemberJoined(ILogger logger, Guid userId, Guid organizationId);
}
