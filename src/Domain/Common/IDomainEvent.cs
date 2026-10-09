namespace Saas.Domain.Common;

/// <summary>
/// Marqueur de domain event. Les events sont écrits dans l'outbox dans la transaction de l'agrégat,
/// puis transmis aux <c>IDomainEventHandler</c> de l'Application par l'<c>OutboxProcessor</c>.
/// </summary>
public interface IDomainEvent;
