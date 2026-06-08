using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberJoined;

public sealed partial class MemberJoinedHandler(ILogger<MemberJoinedHandler> logger)
    : INotificationHandler<MemberJoinedDomainEvent>
{
    public ValueTask Handle(MemberJoinedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        LogMemberJoined(logger, notification.UserId, notification.OrganizationId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Membre rejoint : utilisateur {UserId} sur l'organisation {OrganizationId}")]
    private static partial void LogMemberJoined(ILogger logger, Guid userId, Guid organizationId);
}
