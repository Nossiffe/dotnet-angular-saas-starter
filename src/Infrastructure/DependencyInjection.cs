using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Common;
using Saas.Infrastructure.Identity;
using Saas.Infrastructure.Persistence;
using Saas.Infrastructure.Persistence.Interceptors;

namespace Saas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Providers (stubs Plan 2 — remplacés au Plan 3 par l'auth réelle)
        services.AddScoped<ITenantProvider, StubTenantProvider>();
        services.AddScoped<ICurrentUser, StubCurrentUser>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Default"));
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();

        return services;
    }
}
