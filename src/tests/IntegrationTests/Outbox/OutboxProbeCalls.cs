using System.Collections.Concurrent;

namespace Saas.IntegrationTests.Outbox;

/// <summary>Appels reçus par les handlers de test, par sonde.</summary>
public static class OutboxProbeCalls
{
    public static ConcurrentDictionary<Guid, int> Counting { get; } = new();

    public static ConcurrentDictionary<Guid, int> Failing { get; } = new();

    /// <summary>Jeton d'arrêt du lot, déclenché par une sonde <c>StopsApplication</c>.</summary>
    public static ConcurrentDictionary<Guid, CancellationTokenSource> ApplicationStops { get; } = new();
}
