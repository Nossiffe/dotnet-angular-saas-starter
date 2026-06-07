using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common;
using Saas.Application.Products;
using Saas.Domain.Products;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductConcurrencyTests(PostgresApiFactory factory)
{
    private static readonly Guid _tenant = Guid.Parse("00000000-0000-0000-0000-000000000099");

    private static HttpClient AuthedClient(PostgresApiFactory f)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, _tenant.ToString());
        return client;
    }

    [Fact]
    public async Task Update_concurrent_du_meme_produit_leve_DbUpdateConcurrencyException()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthedClient(factory);

        var name = $"Conc-{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/v1/products", new { name, price = 1m }, ct)).EnsureSuccessStatusCode();
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=100", ct);
        var id = page!.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

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

    [Fact]
    public async Task Update_concurrent_via_endpoint_renvoie_409()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthedClient(factory);

        var name = $"Conc409-{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/v1/products", new { name, price = 1m }, ct)).EnsureSuccessStatusCode();
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=100", ct);
        var id = page!.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        // Deux PUT concurrents sur la même ligne : EF détecte le conflit de xmin sur le 2e save → 409.
        // On rejoue la rafale quelques fois pour neutraliser l'aléa d'ordonnancement.
        HttpStatusCode? conflict = null;
        for (var attempt = 0; attempt < 5 && conflict is null; attempt++)
        {
            var t1 = client.PutAsJsonAsync(
                $"/api/v1/products/{id}", new { name = $"A-{attempt}", price = 2m }, ct);
            var t2 = client.PutAsJsonAsync(
                $"/api/v1/products/{id}", new { name = $"B-{attempt}", price = 3m }, ct);
            var results = await Task.WhenAll(t1, t2);
            conflict = Array.Find(results, r => r.StatusCode == HttpStatusCode.Conflict)?.StatusCode;
        }

        conflict.ShouldBe(HttpStatusCode.Conflict);
    }
}
