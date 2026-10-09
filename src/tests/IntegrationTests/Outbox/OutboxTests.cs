using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Api.Contracts;
using Saas.Domain.Products;
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

    // L'event est écrit dans la transaction de la création, puis un lot le transmet à ses handlers.
    [Fact]
    public async Task A_created_product_event_is_written_to_the_outbox_then_dispatched()
    {
        var ct = TestContext.Current.CancellationToken;
        var create = await AuthedClient(factory).PostAsJsonAsync(
            "/api/v1/products", new { name = $"Outbox-{Guid.NewGuid():N}", price = 4.2m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var productId = (await create.Content.ReadFromJsonAsync<CreateProductResponse>(ct))!.Id.ToString();

        var written = await ProductCreatedMessageAsync(productId, ct);
        written.ProcessedOn.ShouldBeNull();

        await DrainUntilProcessedAsync(written.Id, ct);

        var dispatched = await ProductCreatedMessageAsync(productId, ct);
        dispatched.ProcessedOn.ShouldNotBeNull();
        dispatched.Error.ShouldBeNull();
        dispatched.Attempts.ShouldBe(0);
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

    private OutboxProcessor Processor() => factory.Services.GetRequiredService<OutboxProcessor>();

    private async Task<OutboxMessage> ProductCreatedMessageAsync(string productId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .SingleAsync(m => m.Type.Contains(nameof(ProductCreatedDomainEvent)) && m.Content.Contains(productId), ct);
    }

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

    // Un message retenté n'est repris qu'au lot suivant.
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
