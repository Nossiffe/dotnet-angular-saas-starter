using Saas.Application.Common.Events;

namespace Saas.IntegrationTests.Outbox;

/// <summary>Échoue au premier appel pour une sonde, réussit ensuite.</summary>
public sealed class FailingOnceProbeHandler : IDomainEventHandler<OutboxProbeEvent>
{
    public ValueTask HandleAsync(OutboxProbeEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var calls = OutboxProbeCalls.FailingOnce.AddOrUpdate(domainEvent.ProbeId, 1, (_, n) => n + 1);
        return calls == 1
            ? throw new InvalidOperationException("Échec simulé au premier appel.")
            : ValueTask.CompletedTask;
    }
}
