using System.Diagnostics.CodeAnalysis;
using Saas.Domain.Common;

namespace Saas.Application.Common.Events;

/// <summary>
/// Réagit à un domain event, après coup : l'<c>OutboxProcessor</c> appelle chaque handler une fois
/// l'agrégat enregistré. Un handler qui a réussi n'est pas rappelé quand un autre handler du même
/// event échoue et que l'event est retraité. La livraison reste « au moins une fois » : un handler
/// qui agit hors de la base (e-mail) peut être rappelé après un arrêt brutal.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Vocabulaire DDD établi ; aucun lien avec les délégués EventHandler de .NET.")]
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    ValueTask HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
