using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Domain.Tenancy;

namespace Saas.Application.Tenancy.MemberInvited;

public sealed partial class MemberInvitedHandler(ILogger<MemberInvitedHandler> logger)
    : IDomainEventHandler<MemberInvitedDomainEvent>
{
    public ValueTask HandleAsync(MemberInvitedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        LogMemberInvited(logger, domainEvent.InvitationId.Value, domainEvent.OrganizationId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Membre invité : invitation {InvitationId} sur l'organisation {OrganizationId}")]
    private static partial void LogMemberInvited(ILogger logger, Guid invitationId, Guid organizationId);
}
