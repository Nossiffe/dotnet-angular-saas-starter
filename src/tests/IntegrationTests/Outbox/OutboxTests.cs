using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Saas.Domain.Products;
using Saas.Infrastructure.Jobs;
using Saas.Infrastructure.Persistence;
using Saas.Infrastructure.Persistence.Outbox;
using Saas.IntegrationTests.Auth;
using Saas.IntegrationTests.Products;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Outbox;

[Collection("api")]
public sealed class OutboxTests(PostgresApiFactory factory)
{
    private readonly TestClock _clock = new();

    // L'event est écrit dans la transaction de la création, puis un lot le transmet à ses handlers.
    [Fact]
    public async Task A_created_product_event_is_written_to_the_outbox_then_dispatched()
    {
        var ct = TestContext.Current.CancellationToken;
        var productId = (await factory.CreateAuthenticatedClient()
            .CreateProductAsync($"Outbox-{Guid.NewGuid():N}", 4.2m, ct)).ToString();

        var written = await ProductCreatedMessageAsync(productId, ct);
        written.ProcessedOn.ShouldBeNull();

        await DrainUntilProcessedAsync(written.Id, ct);

        var dispatched = await ProductCreatedMessageAsync(productId, ct);
        dispatched.ProcessedOn.ShouldNotBeNull();
        dispatched.Error.ShouldBeNull();
        dispatched.Attempts.ShouldBe(0);
    }

    // Un handler a échoué, l'autre a réussi : le message est repris une minute plus tard, pas avant,
    // et seul le handler qui a échoué est rappelé.
    [Fact]
    public async Task A_failed_message_is_retried_later_without_calling_again_the_handlers_that_succeeded()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = new OutboxProbeEvent(Guid.CreateVersion7(), Failures: 1);
        var messageId = await InsertAsync(probe, ct);
        var firstRun = _clock.UtcNow;

        await RunBatchesUntilIdleAsync(ct);
        var failed = await MessageAsync(messageId, ct);
        _clock.Advance(TimeSpan.FromSeconds(59));
        await RunBatchesUntilIdleAsync(ct);
        var callsBeforeTheDelay = OutboxProbeCalls.Failing[probe.ProbeId];
        _clock.Advance(TimeSpan.FromSeconds(1));
        await RunBatchesUntilIdleAsync(ct);

        failed.Attempts.ShouldBe(1);
        failed.NextAttemptOn.ShouldBe(firstRun + TimeSpan.FromMinutes(1));
        callsBeforeTheDelay.ShouldBe(1);
        OutboxProbeCalls.Failing[probe.ProbeId].ShouldBe(2);
        OutboxProbeCalls.Counting[probe.ProbeId].ShouldBe(1);
        var processed = await MessageAsync(messageId, ct);
        processed.ProcessedOn.ShouldNotBeNull();
        processed.Attempts.ShouldBe(1);
        (await ConsumerCountAsync(messageId, ct)).ShouldBe(2);
    }

    // Un message qui échoue toujours est repris 1, 4, 16 puis 64 minutes plus tard, puis abandonné à
    // la cinquième tentative et journalisé en erreur.
    [Fact]
    public async Task A_message_that_keeps_failing_is_abandoned_after_five_attempts()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = new OutboxProbeEvent(Guid.CreateVersion7(), Failures: int.MaxValue);
        var messageId = await InsertAsync(probe, ct);

        var delays = new List<TimeSpan?>();
        for (var attempt = 1; attempt <= OutboxProcessor.MaxAttempts; attempt++)
        {
            await RunBatchesUntilIdleAsync(ct);
            var message = await MessageAsync(messageId, ct);
            delays.Add(message.NextAttemptOn - _clock.UtcNow);
            _clock.UtcNow = message.NextAttemptOn ?? _clock.UtcNow;
        }

        _clock.Advance(TimeSpan.FromDays(1));
        await RunBatchesUntilIdleAsync(ct);

        delays.ShouldBe([
            TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(16), TimeSpan.FromMinutes(64), null]);
        OutboxProbeCalls.Failing[probe.ProbeId].ShouldBe(OutboxProcessor.MaxAttempts);
        var abandoned = await MessageAsync(messageId, ct);
        abandoned.ProcessedOn.ShouldBeNull();
        abandoned.Attempts.ShouldBe(OutboxProcessor.MaxAttempts);
        factory.Logs.Entries.ShouldContain(e => e.Level == LogLevel.Error
            && e.Category.EndsWith(nameof(OutboxProcessor), StringComparison.Ordinal)
            && e.Message.Contains(messageId.ToString(), StringComparison.Ordinal));
    }

    // L'issue du message n'a pas pu être enregistrée : la tentative est comptée à part. Sans cela, le
    // même message serait repris à chaque lot, indéfiniment.
    [Fact]
    public async Task A_message_whose_outcome_cannot_be_saved_still_counts_an_attempt()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = new OutboxProbeEvent(Guid.CreateVersion7(), FailsToSave: true);
        var messageId = await InsertAsync(probe, ct);

        await RunBatchesUntilIdleAsync(ct);

        var message = await MessageAsync(messageId, ct);
        message.ProcessedOn.ShouldBeNull();
        message.Attempts.ShouldBe(1);
        message.NextAttemptOn.ShouldBe(_clock.UtcNow + TimeSpan.FromMinutes(1));
        OutboxProbeCalls.Counting[probe.ProbeId].ShouldBe(1);
    }

    // L'application s'arrête pendant un handler : le message en cours reste à traiter, sans tentative
    // comptée, et le message validé juste avant reste validé. Au redémarrage, le message est repris.
    [Fact]
    public async Task An_application_stop_keeps_the_messages_already_processed()
    {
        var ct = TestContext.Current.CancellationToken;
        var firstId = await InsertAsync(new OutboxProbeEvent(Guid.CreateVersion7()), ct);
        var interrupted = new OutboxProbeEvent(Guid.CreateVersion7(), StopsApplication: true);
        var interruptedId = await InsertAsync(interrupted, ct);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        OutboxProbeCalls.ApplicationStops[interrupted.ProbeId] = stop;

        await Should.ThrowAsync<OperationCanceledException>(() => RunBatchesUntilIdleAsync(stop.Token));

        (await MessageAsync(firstId, ct)).ProcessedOn.ShouldNotBeNull();
        var pending = await MessageAsync(interruptedId, ct);
        pending.ProcessedOn.ShouldBeNull();
        pending.Attempts.ShouldBe(0);
        pending.Error.ShouldBeNull();
        (await ConsumerCountAsync(interruptedId, ct)).ShouldBe(0);
        await DrainUntilProcessedAsync(interruptedId, ct);
    }

    // Une autre instance traite déjà le message (ligne verrouillée) : ce lot la saute, sans attendre.
    [Fact]
    public async Task A_message_locked_by_another_instance_is_skipped()
    {
        var ct = TestContext.Current.CancellationToken;
        var probe = new OutboxProbeEvent(Guid.CreateVersion7());
        var messageId = await InsertAsync(probe, ct);

        await using (var otherInstance = factory.Services.CreateAsyncScope())
        {
            var otherDb = otherInstance.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await otherDb.Database.BeginTransactionAsync(ct);
            await otherDb.Database.ExecuteSqlAsync(
                $"""SELECT 1 FROM "OutboxMessages" WHERE "Id" = {messageId} FOR UPDATE""", ct);

            await RunBatchesUntilIdleAsync(ct);

            OutboxProbeCalls.Counting.ContainsKey(probe.ProbeId).ShouldBeFalse();
            await transaction.RollbackAsync(ct);
        }

        await DrainUntilProcessedAsync(messageId, ct);
        OutboxProbeCalls.Counting[probe.ProbeId].ShouldBe(1);
    }

    // La purge supprime les messages traités depuis plus de 7 jours, avec leurs consommateurs. Elle
    // garde les messages traités récemment et les messages abandonnés.
    [Fact]
    public async Task The_purge_deletes_only_the_messages_processed_more_than_seven_days_ago()
    {
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;
        var old = await InsertAsync(new OutboxProbeEvent(Guid.CreateVersion7()), ct, m => m.ProcessedOn = now.AddDays(-8));
        var recent = await InsertAsync(new OutboxProbeEvent(Guid.CreateVersion7()), ct, m => m.ProcessedOn = now.AddDays(-6));
        var abandoned = await InsertAsync(new OutboxProbeEvent(Guid.CreateVersion7()), ct, m =>
        {
            m.OccurredOn = now.AddDays(-30);
            m.Attempts = OutboxProcessor.MaxAttempts;
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OutboxMessageConsumers.Add(new OutboxMessageConsumer { MessageId = old, Handler = "Handler" });
            await db.SaveChangesAsync(ct);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PurgeProcessedOutboxMessagesJob>().RunAsync();
        }

        await using var check = factory.Services.CreateAsyncScope();
        var checkDb = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await checkDb.OutboxMessages.Where(m => m.Id == old || m.Id == recent || m.Id == abandoned)
            .Select(m => m.Id).ToListAsync(ct))
            .ShouldBe([recent, abandoned], ignoreOrder: true);
        (await ConsumerCountAsync(old, ct)).ShouldBe(0);
    }

    // Lots à l'heure du test, que le test avance.
    private Task RunBatchesUntilIdleAsync(CancellationToken ct) => OutboxRunner.RunUntilIdleAsync(factory, _clock, ct);

    // Avance l'horloge jusqu'à la tentative suivante tant que le message n'est pas traité.
    private async Task DrainUntilProcessedAsync(Guid messageId, CancellationToken ct)
    {
        for (var attempt = 0; attempt < OutboxProcessor.MaxAttempts; attempt++)
        {
            await RunBatchesUntilIdleAsync(ct);
            if ((await MessageAsync(messageId, ct)).ProcessedOn is not null)
            {
                return;
            }

            _clock.Advance(TimeSpan.FromHours(2));
        }

        throw new InvalidOperationException($"Message {messageId} non traité.");
    }

    private async Task<Guid> InsertAsync(
        OutboxProbeEvent probe, CancellationToken ct, Action<OutboxMessage>? configure = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = new OutboxMessage
        {
            Type = typeof(OutboxProbeEvent).AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(probe, OutboxJson.Options),
            OccurredOn = DateTimeOffset.UtcNow,
        };
        configure?.Invoke(message);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(ct);
        return message.Id;
    }

    private async Task<OutboxMessage> MessageAsync(Guid messageId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .SingleAsync(m => m.Id == messageId, ct);
    }

    private async Task<OutboxMessage> ProductCreatedMessageAsync(string productId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessages
            .AsNoTracking()
            .SingleAsync(m => m.Type.Contains(nameof(ProductCreatedDomainEvent)) && m.Content.Contains(productId), ct);
    }

    private async Task<int> ConsumerCountAsync(Guid messageId, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().OutboxMessageConsumers
            .CountAsync(c => c.MessageId == messageId, ct);
    }
}
