using Saas.Domain.Common;

namespace Saas.IntegrationTests.Outbox;

/// <summary>Event de test, écrit directement dans l'outbox par les tests.</summary>
public sealed record OutboxProbeEvent(Guid ProbeId) : IDomainEvent;
