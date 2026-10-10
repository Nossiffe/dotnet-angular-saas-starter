using Saas.Application.Common.Events;

namespace Saas.IntegrationTests.Outbox;

public sealed class CountingProbeHandler : IDomainEventHandler<OutboxProbeEvent>
{
    public ValueTask HandleAsync(OutboxProbeEvent domainEvent, CancellationToken cancellationToken)
    {
        OutboxProbeCalls.Counting.AddOrUpdate(domainEvent.ProbeId, 1, (_, n) => n + 1);
        return ValueTask.CompletedTask;
    }
}
