using Saas.Application.Common.Interfaces;

namespace Saas.IntegrationTests.Outbox;

/// <summary>
/// Horloge que le test avance lui-même, pour atteindre la date d'une nouvelle tentative. Arrondie à
/// la milliseconde : PostgreSQL stocke les dates à la microseconde.
/// </summary>
public sealed class TestClock : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; } =
        DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    public void Advance(TimeSpan delay) => UtcNow += delay;
}
