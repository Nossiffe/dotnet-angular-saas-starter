namespace Saas.Infrastructure.Persistence.Outbox;

// Entité d'infrastructure (PAS un agrégat) : ligne de la table outbox.
// Sérialisée dans la MÊME transaction que l'agrégat (atomicité) par le DomainEventsInterceptor,
// puis dispatchée de façon fiable par l'OutboxProcessor (BackgroundService).
internal sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // AssemblyQualifiedName du domain event — résolu via Type.GetType au dispatch.
    public required string Type { get; set; }

    // JSON de l'event (STJ, options partagées écriture/lecture via OutboxJson).
    public required string Content { get; set; }

    public DateTimeOffset OccurredOn { get; set; }

    public DateTimeOffset? ProcessedOn { get; set; }

    public string? Error { get; set; }

    public int Attempts { get; set; }

    // Après un échec, le message n'est pas repris avant cette date. Null : à traiter dès que possible.
    public DateTimeOffset? NextAttemptOn { get; set; }
}
