using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Saas.Domain.Products;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductConcurrencyTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Concurrent_updates_of_a_product_raise_a_concurrency_exception()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await factory.CreateAuthenticatedClient().CreateProductAsync($"Conc-{Guid.NewGuid():N}", 1m, ct);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Charge l'entité (xmin courant capturé sur l'entité suivie).
        var tracked = await db.Products.IgnoreQueryFilters()
            .FirstAsync(p => p.Id == ProductId.From(id), ct);
        tracked.Rename("Renommé par le contexte A");

        // Une autre opération modifie la même ligne → Postgres incrémente xmin.
        await db.Database.ExecuteSqlRawAsync(
            """UPDATE "Products" SET "Name" = 'Renommé en SQL brut' WHERE "Id" = {0}""",
            [id],
            ct);

        // Le save du contexte A travaille sur un xmin périmé → conflit.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(async () => await db.SaveChangesAsync(ct));
    }

    // Une autre transaction modifie le produit entre la lecture et l'écriture de la requête. La
    // requête attend la fin de cette transaction sur le verrou de la ligne, puis son UPDATE ne
    // trouve plus la version lue (xmin) : 409. Le test attend que la requête soit bloquée avant de
    // valider l'autre transaction, si bien que l'ordre des opérations est toujours le même.
    [Fact]
    public async Task A_concurrent_write_makes_the_update_return_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateAuthenticatedClient();
        var id = await client.CreateProductAsync($"Conc409-{Guid.NewGuid():N}", 1m, ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var concurrentWrite = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"""UPDATE "Products" SET "Name" = 'Changed elsewhere' WHERE "Id" = {id}""", ct);
        var concurrentWriter = ((NpgsqlConnection)db.Database.GetDbConnection()).ProcessID;

        var update = client.PutAsJsonAsync($"/api/v1/products/{id}", new { name = "Renamed", price = 2m }, ct);
        await WaitUntilAQueryIsBlockedByAsync(concurrentWriter, ct);
        await concurrentWrite.CommitAsync(ct);

        (await update).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    // Une requête attend un verrou que tient la connexion du processus serveur indiqué.
    private async Task WaitUntilAQueryIsBlockedByAsync(int blockingPid, CancellationToken ct)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await db.Database.SqlQuery<bool>($"""
                    SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE {blockingPid} = ANY(pg_blocking_pids(pid))) AS "Value"
                    """)
                .SingleAsync(ct))
            {
                return;
            }

            await Task.Delay(50, ct);
        }

        throw new InvalidOperationException("La requête n'a jamais attendu le verrou de l'autre transaction.");
    }
}
