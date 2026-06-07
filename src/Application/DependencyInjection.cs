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

        return services;
    }
}
