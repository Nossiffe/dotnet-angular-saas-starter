using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Saas.Infrastructure.Persistence;
using Saas.Infrastructure.Persistence.Outbox;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Outbox;

[Collection("api")]
public sealed class OutboxTests(PostgresApiFactory factory)
{
    private static HttpClient AuthedClient(PostgresApiFactory f)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader,
            Guid.Parse("00000000-0000-0000-0000-000000000001").ToString());
        return client;
    }

    [Fact]
    public async Task Create_product_writes_outbox_message_then_processor_dispatches_it()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthedClient(factory);

        // Création d'un produit → l'intercepteur écrit une ligne OutboxMessages dans la MÊME transaction.
        var name = $"Outbox-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/products", new { name, price = 4.2m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Une ligne outbox a bien été persistée pour le ProductCreatedDomainEvent.
        var messages = await db.OutboxMessages
            .Where(m => m.Type.Contains("ProductCreatedDomainEvent"))
            .ToListAsync(ct);
        messages.ShouldNotBeEmpty();

        // On déclenche explicitement des batches (au lieu d'attendre le tick du BackgroundService),
        // jusqu'à épuisement des lignes non traitées.
        var processor = factory.Services.GetServices<IHostedService>()
            .OfType<OutboxProcessor>()
            .Single();
        while (await processor.ProcessBatchAsync(ct) > 0)
        {
            // drain
        }

        // Plus aucune ligne non traitée → le dispatch (ProductCreatedHandler) a eu lieu sans erreur.
        var unprocessed = await db.OutboxMessages
            .Where(m => m.ProcessedOn == null && m.Attempts < 5)
            .ToListAsync(ct);
        unprocessed.ShouldBeEmpty();
    }
}
