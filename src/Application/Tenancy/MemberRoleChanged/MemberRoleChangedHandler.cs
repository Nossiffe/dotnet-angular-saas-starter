using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberRoleChanged;

public sealed partial class MemberRoleChangedHandler(ILogger<MemberRoleChangedHandler> logger)
    : INotificationHandler<MemberRoleChangedDomainEvent>
{
    public ValueTask Handle(MemberRoleChangedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        LogMemberRoleChanged(logger, notification.UserId, notification.OrganizationId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rôle de membre changé : utilisateur {UserId} sur l'organisation {OrganizationId}")]
    private static partial void LogMemberRoleChanged(ILogger logger, Guid userId, Guid organizationId);
}
