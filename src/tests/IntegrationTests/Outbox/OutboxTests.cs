using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        // Horodatage avant le POST pour isoler les lignes outbox propres à CE test
        // (d'autres tests de la même collection créent aussi des produits).
        var testStart = DateTimeOffset.UtcNow.AddMilliseconds(-1);

        // Création d'un produit → l'intercepteur écrit une ligne OutboxMessages dans la MÊME transaction.
        var name = $"Outbox-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/v1/products", new { name, price = 4.2m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Vérification 1 : la ligne outbox a bien été persistée.
        await using (var scope1 = factory.Services.CreateAsyncScope())
        {
            var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
            var messages = await db1.OutboxMessages
                .Where(m => m.Type.Contains("ProductCreatedDomainEvent") && m.OccurredOn >= testStart)
                .AsNoTracking()
                .ToListAsync(ct);
            messages.ShouldNotBeEmpty();
        }

        // On déclenche explicitement des batches (au lieu d'attendre le tick du BackgroundService),
        // jusqu'à épuisement des lignes non traitées.
        var processor = factory.Services.GetServices<IHostedService>()
            .OfType<OutboxProcessor>()
            .Single();
        while (await processor.ProcessBatchAsync(ct) > 0)
        {
            // drain
        }

        // Vérification 2 : nouveau scope DbContext pour lire l'état APRÈS le drain (évite le cache
        // de l'identity map EF qui retournerait les entités pré-drain de scope1).
        await using var scope2 = factory.Services.CreateAsyncScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        // Plus aucune ligne non traitée → le dispatch (ProductCreatedHandler) a eu lieu sans erreur.
        var unprocessed = await db2.OutboxMessages
            .Where(m => m.ProcessedOn == null && m.Attempts < 5)
            .AsNoTracking()
            .ToListAsync(ct);
        unprocessed.ShouldBeEmpty();

        // Assertion forte : les messages ProductCreated de CE test ont été effectivement dispatchés
        // (ProcessedOn non-null, aucune erreur). Sans cela, l'assertion précédente passerait aussi si
        // un message avait épuisé ses 5 tentatives sans succès.
        var processed = await db2.OutboxMessages
            .Where(m => m.Type.Contains("ProductCreatedDomainEvent") && m.OccurredOn >= testStart)
            .AsNoTracking()
            .ToListAsync(ct);
        processed.ShouldNotBeEmpty();
        processed.ShouldAllBe(m => m.ProcessedOn != null && m.Error == null);
    }

    // Un handler a échoué, l'autre a réussi : au nouvel essai, seul celui qui a échoué est rappelé.
    [Fact]
    public async Task A_retried_message_does_not_call_again_the_handlers_that_succeeded()
    {
        var ct = TestContext.Current.CancellationToken;
        var probeId = Guid.CreateVersion7();
        var messageId = await InsertProbeAsync(probeId, ct);

        await DrainUntilProcessedAsync(messageId, ct);

        OutboxProbeCalls.FailingOnce[probeId].ShouldBe(2);
        OutboxProbeCalls.Counting[probeId].ShouldBe(1);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId, ct);
        message.Attempts.ShouldBe(1);
        (await db.OutboxMessageConsumers.CountAsync(c => c.MessageId == messageId, ct)).ShouldBe(2);
    }

    // Une autre instance traite déjà le message (ligne verrouillée) : ce lot la saute, sans attendre.
    [Fact]
    public async Task A_message_locked_by_another_instance_is_skipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var probeId = Guid.CreateVersion7();
        var messageId = await InsertProbeAsync(probeId, ct);

        await using (var otherInstance = factory.Services.CreateAsyncScope())
        {
            var otherDb = otherInstance.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await otherDb.Database.BeginTransactionAsync(ct);
            await otherDb.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM "OutboxMessages" WHERE "Id" = {messageId} FOR UPDATE""", ct);

            await Processor().ProcessBatchAsync(ct);

            OutboxProbeCalls.Counting.ContainsKey(probeId).ShouldBeFalse();
            await transaction.RollbackAsync(ct);
        }

        await DrainUntilProcessedAsync(messageId, ct);
        OutboxProbeCalls.Counting[probeId].ShouldBe(1);
    }

    private OutboxProcessor Processor()
        => factory.Services.GetServices<IHostedService>().OfType<OutboxProcessor>().Single();

    private async Task<Guid> InsertProbeAsync(Guid probeId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = new OutboxMessage
        {
            Type = typeof(OutboxProbeEvent).AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(new OutboxProbeEvent(probeId), OutboxJson.Options),
            OccurredOn = DateTimeOffset.UtcNow,
        };
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message.Id;
    }

    // Le processeur de fond tourne aussi pendant les tests : on relance des lots jusqu'à ce que le
    // message soit traité, quel que soit le lot qui l'a pris.
    private async Task DrainUntilProcessedAsync(Guid messageId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await Processor().ProcessBatchAsync(ct);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await db.OutboxMessages.AnyAsync(m => m.Id == messageId && m.ProcessedOn != null, ct))
            {
                return;
            }
        }

        throw new InvalidOperationException($"Message {messageId} non traité après 10 lots.");
    }
}
