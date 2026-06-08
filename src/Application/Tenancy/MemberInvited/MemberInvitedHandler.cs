using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberInvited;

public sealed partial class MemberInvitedHandler(ILogger<MemberInvitedHandler> logger)
    : INotificationHandler<MemberInvitedDomainEvent>
{
    public ValueTask Handle(MemberInvitedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        LogMemberInvited(logger, notification.InvitationId.Value, notification.OrganizationId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Membre invité : invitation {InvitationId} sur l'organisation {OrganizationId}")]
    private static partial void LogMemberInvited(ILogger logger, Guid invitationId, Guid organizationId);
}
