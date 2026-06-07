using System.Reflection;
using Asp.Versioning;

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

        // Groupe versionné racine : /api/v{version:apiVersion}
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        var api = app
            .MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(versionSet)
            .HasApiVersion(new ApiVersion(1, 0));

        foreach (var endpoint in app.Services.GetServices<IEndpoint>())
        {
            endpoint.MapEndpoint(api);
        }
    }
}
