using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Events;

namespace Saas.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly,
            ServiceLifetime.Transient,
            includeInternalTypes: true);
        AddDomainEventHandlers(services);

        // HybridCache (.NET 10) — L1 mémoire par défaut. Pour un L2 distribué, ajouter
        // Microsoft.Extensions.Caching.StackExchangeRedis + AddStackExchangeRedisCache(...) :
        // HybridCache l'utilisera automatiquement (voir ARCHITECTURE.md).
#pragma warning disable EXTEXP0018 // HybridCache est stable en .NET 10 ; l'API garde l'attribut Experimental sur certains overloads
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services;
    }

    // Chaque IDomainEventHandler<TEvent> de l'assembly, enregistré sous son interface : l'outbox
    // résout tous les handlers d'un event par IEnumerable<IDomainEventHandler<TEvent>>.
    private static void AddDomainEventHandlers(IServiceCollection services)
    {
        var handlers = typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlers)
        {
            services.AddScoped(service, implementation);
        }
    }
}
