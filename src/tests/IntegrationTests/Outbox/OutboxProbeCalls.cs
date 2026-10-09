using System.Collections.Concurrent;

namespace Saas.IntegrationTests.Outbox;

/// <summary>Appels reçus par les handlers de test, par sonde.</summary>
public static class OutboxProbeCalls
{
    public static ConcurrentDictionary<Guid, int> Counting { get; } = new();

    public static ConcurrentDictionary<Guid, int> FailingOnce { get; } = new();
}
