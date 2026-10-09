using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common;
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
        var create = await client.PostAsJsonAsync("/api/v1/products", new { name, price = 10m }, ct);
        create.EnsureSuccessStatusCode();

        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        var id = page!.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        // 1er GET → peuple le cache (GetProductByIdHandler).
        var first = await client.GetFromJsonAsync<ProductDto>($"/api/v1/products/{id}", ct);
        first!.Price.ShouldBe(10m);

        // On modifie le prix directement en base, par-dessus la tête du cache (pas d'invalidation).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlAsync(
                $"""UPDATE "Products" SET "Price_Amount" = 99 WHERE "Id" = {id}""",
                ct);
        }

        // 2e GET → toujours servi par le cache → ancienne valeur.
        var cached = await client.GetFromJsonAsync<ProductDto>($"/api/v1/products/{id}", ct);
        cached!.Price.ShouldBe(10m);

        // Update via l'endpoint → invalidation par tag (ProductCache.Tag du tenant).
        var update = await client.PutAsJsonAsync($"/api/v1/products/{id}", new { name, price = 42m }, ct);
        update.EnsureSuccessStatusCode();

        // GET après invalidation → re-fetch depuis la base → nouvelle valeur.
        var afterInvalidation = await client.GetFromJsonAsync<ProductDto>($"/api/v1/products/{id}", ct);
        afterInvalidation!.Price.ShouldBe(42m);
    }
}
