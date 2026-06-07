using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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

        // HybridCache (.NET 10) — L1 mémoire par défaut. Pour un L2 distribué, ajouter
        // Microsoft.Extensions.Caching.StackExchangeRedis + AddStackExchangeRedisCache(...) :
        // HybridCache l'utilisera automatiquement (voir ARCHITECTURE.md).
#pragma warning disable EXTEXP0018 // HybridCache est stable en .NET 10 ; l'API garde l'attribut Experimental sur certains overloads
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services;
    }
}
