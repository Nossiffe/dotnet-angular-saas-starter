using System.Net;
using System.Net.Http.Json;
using Saas.Application.Common;
using Saas.Application.Products;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductsEndpointTests(PostgresApiFactory factory)
{
    private static HttpClient AuthedClient(PostgresApiFactory f, Guid? tenant = null)
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader,
            (tenant ?? Guid.Parse("00000000-0000-0000-0000-000000000001")).ToString());
        return client;
    }

    [Fact]
    public async Task Create_then_get_list_update_delete()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthedClient(factory);

        var name = $"Stylo-{Guid.NewGuid():N}";

        // Create
        var create = await client.PostAsJsonAsync("/api/v1/products", new { name, price = 2.5m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        // List (Dapper) — paginated response
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        page.ShouldNotBeNull();
        page!.Items.ShouldContain(p => string.Equals(p.Name, name, StringComparison.Ordinal));
        var id = page.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        // Get by id
        var got = await client.GetFromJsonAsync<ProductDto>($"/api/v1/products/{id}", ct);
        got!.Price.ShouldBe(2.5m);

        // Update
        var update = await client.PutAsJsonAsync($"/api/v1/products/{id}", new { name = "Stylo bleu", price = 3m }, ct);
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Delete (soft) — admin claim in TestAuthHandler, so TenantAdmin policy passes
        var delete = await client.DeleteAsync($"/api/v1/products/{id}", ct);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Après soft-delete : absent de la liste
        var after = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        after!.Items.ShouldNotContain(p => p.Id == id);
    }

    [Fact]
    public async Task Create_invalide_renvoie_400_ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = AuthedClient(factory);
        var resp = await client.PostAsJsonAsync("/api/v1/products", new { name = "", price = -1m }, ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync(ct);
        body.ShouldContain("errors");
    }

    [Fact]
    public async Task Pagination_page1_pageSize2_returns_correct_items_and_total()
    {
        var ct = TestContext.Current.CancellationToken;
        // Use a fresh tenant so we control exactly what products exist.
        var tenant = Guid.CreateVersion7();
        var client = AuthedClient(factory, tenant);

        // Create 4 products
        var names = Enumerable.Range(1, 4).Select(i => $"ProdPag-{i:D2}-{Guid.NewGuid():N}").ToList();
        foreach (var n in names)
        {
            (await client.PostAsJsonAsync("/api/v1/products", new { name = n, price = 1m }, ct))
                .EnsureSuccessStatusCode();
        }

        // Page 1, pageSize 2 → exactly 2 items + totalCount = 4
        var page1 = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?page=1&pageSize=2", ct);
        page1.ShouldNotBeNull();
        page1!.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(4);
        page1.TotalPages.ShouldBe(2);
        page1.Page.ShouldBe(1);
        page1.PageSize.ShouldBe(2);

        // Page 2, pageSize 2 → 2 items
        var page2 = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?page=2&pageSize=2", ct);
        page2.ShouldNotBeNull();
        page2!.Items.Count.ShouldBe(2);
        page2.TotalCount.ShouldBe(4);
    }
}
