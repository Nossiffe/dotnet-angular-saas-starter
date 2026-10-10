using Hangfire;
using Saas.Api.Common;

namespace Saas.Api.Hosting;

/// <summary>Serveur Hangfire, tableau de bord et jobs récurrents ; ni serveur ni jobs pendant la génération OpenAPI au build.</summary>
internal static class BackgroundJobsExtensions
{
    public static IServiceCollection AddBackgroundJobServer(this IServiceCollection services)
    {
        if (!BuildTimeOpenApi.IsRunning)
        {
            services.AddHangfireServer();
        }

        return services;
    }

    public static WebApplication UseBackgroundJobs(this WebApplication app)
    {
        // Servi au navigateur : accès contrôlé par son propre filtre, pas par l'autorisation JWT.
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new HangfireDashboardAuthFilter(app.Environment)],
        });

        if (!BuildTimeOpenApi.IsRunning)
        {
            Saas.Infrastructure.DependencyInjection.RegisterRecurringJobs(app.Services);
        }

        return app;
    }
}
