namespace Saas.Infrastructure.Persistence.Outbox;

// Trace qu'un handler a traité un message de l'outbox. Quand un autre handler du même message
// échoue et que le message est retraité, les handlers déjà passés ne sont pas rappelés.
internal sealed class OutboxMessageConsumer
{
    public required Guid MessageId { get; init; }

    // Nom complet du type du handler.
    public required string Handler { get; init; }
}
