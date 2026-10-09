using Saas.Domain.Common;

namespace Saas.IntegrationTests.Outbox;

/// <summary>Event de test, écrit directement dans l'outbox par les tests.</summary>
/// <param name="ProbeId">Identifie la sonde dans <see cref="OutboxProbeCalls"/>.</param>
/// <param name="Failures">Nombre de premiers appels de <see cref="FailingProbeHandler"/> qui échouent.</param>
/// <param name="StopsApplication">Le handler simule un arrêt de l'application pendant le lot.</param>
/// <param name="FailsToSave">L'issue du message ne peut pas être enregistrée (<see cref="UnsavableProbeInterceptor"/>).</param>
public sealed record OutboxProbeEvent(
    Guid ProbeId,
    int Failures = 0,
    bool StopsApplication = false,
    bool FailsToSave = false) : IDomainEvent;
