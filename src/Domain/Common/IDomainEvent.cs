using Mediator;

namespace Saas.Domain.Common;

/// <summary>Marqueur de domain event, dispatché via Mediator après <c>SaveChanges</c>.</summary>
public interface IDomainEvent : INotification;
