using Saas.Application.Common.Events;

namespace Saas.IntegrationTests.Outbox;

/// <summary>Échoue aux premiers appels d'une sonde, ou arrête le lot une fois, selon la sonde.</summary>
public sealed class FailingProbeHandler : IDomainEventHandler<OutboxProbeEvent>
{
    public ValueTask HandleAsync(OutboxProbeEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        var calls = OutboxProbeCalls.Failing.AddOrUpdate(domainEvent.ProbeId, 1, (_, n) => n + 1);
        if (domainEvent.StopsApplication && OutboxProbeCalls.ApplicationStops.TryRemove(domainEvent.ProbeId, out var stop))
        {
            stop.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return calls <= domainEvent.Failures
            ? throw new InvalidOperationException($"Échec simulé, appel {calls}.")
            : ValueTask.CompletedTask;
    }
}
