using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Domain.Common;

namespace Saas.Infrastructure.Persistence.Interceptors;

// Après SaveChanges, collecte les domain events des AggregateRoot<> trackés et les publie via Mediator.
//
// Contraintes à connaître (par conception) :
//  - Un domain event n'est dispatché que lors d'un SaveChanges : lever un événement sans sauver = jamais publié.
//  - Le dispatch passe par le générateur source de Mediator : l'assembly qui DÉFINIT le type d'événement
//    doit figurer dans `options.AddMediator(... Assemblies ...)` (ici l'assembly Domain est inclus), sinon
//    l'événement est silencieusement ignoré (pas de handler généré → no-op).
public sealed class DomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        if (eventData.Context is not null)
        {
            await DispatchAsync(eventData.Context, cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregates = context.ChangeTracker
            .Entries()
            .Select(e => e.Entity)
            .OfType<IHasDomainEvents>()
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        foreach (var aggregate in aggregates)
        {
            aggregate.ClearDomainEvents();
        }

        foreach (var domainEvent in events)
        {
            // Cast en object pour forcer la résolution du handler sur le TYPE concret
            // (ex. ProductCreatedDomainEvent) plutôt que sur IDomainEvent.
            await publisher.Publish((object)domainEvent, cancellationToken);
        }
    }
}
