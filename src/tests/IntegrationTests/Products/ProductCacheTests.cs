using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Products;
using Saas.Infrastructure.Persistence;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductCacheTests(PostgresApiFactory factory)
{
    private static HttpClient AuthedClient(PostgresApiFactory f)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader,
            Guid.Parse("00000000-0000-0000-0000-000000000001").ToString());
        return client;
    }

    [Fact]
    public async Task GetById_is_cached_and_invalidated_on_update()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthedClient(factory);

        var name = $"Cache-{Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/products", new { name, price = 10m }, ct);
        create.EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<List<ProductDto>>("/products", ct);
        var id = list!.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        // 1er GET → peuple le cache (clé product:{tenant}:{id}).
        var first = await client.GetFromJsonAsync<ProductDto>($"/products/{id}", ct);
        first!.Price.ShouldBe(10m);

        // On modifie le prix DIRECTEMENT en base, par-dessus la tête du cache (pas d'invalidation).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"""UPDATE "Products" SET "Price_Amount" = 99 WHERE "Id" = {id}""",
                ct);
        }

        // 2e GET → toujours servi par le CACHE → ancienne valeur.
        var cached = await client.GetFromJsonAsync<ProductDto>($"/products/{id}", ct);
        cached!.Price.ShouldBe(10m);

        // Update via l'endpoint → invalidation (RemoveAsync).
        var update = await client.PutAsJsonAsync($"/products/{id}", new { name, price = 42m }, ct);
        update.EnsureSuccessStatusCode();

        // GET après invalidation → re-fetch depuis la base → nouvelle valeur.
        var afterInvalidation = await client.GetFromJsonAsync<ProductDto>($"/products/{id}", ct);
        afterInvalidation!.Price.ShouldBe(42m);
    }
}
