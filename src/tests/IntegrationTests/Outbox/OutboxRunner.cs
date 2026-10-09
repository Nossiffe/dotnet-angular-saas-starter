using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Saas.Application.Common.Interfaces;
using Saas.Infrastructure.Persistence.Outbox;

namespace Saas.IntegrationTests.Outbox;

/// <summary>
/// Lance les lots de l'outbox à la place d'OutboxPollingService, retiré des tests : les e-mails
/// d'invitation, par exemple, ne partent qu'avec un lot.
/// </summary>
internal static class OutboxRunner
{
    /// <summary>Le processeur de l'application, avec l'horloge donnée.</summary>
    public static OutboxProcessor Processor(PostgresApiFactory factory, IDateTimeProvider clock) => new(
        factory.Services.GetRequiredService<IServiceScopeFactory>(),
        clock,
        factory.Services.GetRequiredService<ILogger<OutboxProcessor>>());

    /// <summary>
    /// Lots successifs jusqu'à ce qu'aucun message ne soit dû. Les messages des autres tests de la
    /// collection sont traités au passage.
    /// </summary>
    public static async Task RunUntilIdleAsync(PostgresApiFactory factory, IDateTimeProvider clock, CancellationToken ct)
    {
        var processor = Processor(factory, clock);
        while (await processor.ProcessBatchAsync(ct) > 0)
        {
            // Lot suivant.
        }
    }

    /// <summary>Lots à l'heure de l'application.</summary>
    public static Task RunUntilIdleAsync(PostgresApiFactory factory, CancellationToken ct)
        => RunUntilIdleAsync(factory, factory.Services.GetRequiredService<IDateTimeProvider>(), ct);
}
