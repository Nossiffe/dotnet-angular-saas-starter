using System.Reflection;

namespace Saas.Api.Common;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var endpoints = Assembly.GetExecutingAssembly().DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                && typeof(IEndpoint).IsAssignableFrom(t))
            .Select(t => (IEndpoint)(Activator.CreateInstance(t)
                ?? throw new InvalidOperationException(
                    $"Impossible d'instancier l'endpoint {t.FullName} : un constructeur public sans paramètre est requis.")));

        foreach (var endpoint in endpoints)
        {
            services.AddSingleton(endpoint);
        }

        return services;
    }

    public static void MapEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        foreach (var endpoint in app.Services.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(app);
        }
    }
}
