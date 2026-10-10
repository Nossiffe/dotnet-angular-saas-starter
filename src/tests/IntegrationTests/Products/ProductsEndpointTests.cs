using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Saas.Application.Common;
using Saas.Application.Products;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductsEndpointTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task A_created_product_is_readable_at_its_location_and_listed()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateAuthenticatedClient();
        var name = $"Stylo-{Guid.NewGuid():N}";

        var id = await client.CreateProductAsync(name, 2.5m, ct);

        var product = await client.GetFromJsonAsync<ProductDto>($"/api/v1/products/{id}", ct);
        product.ShouldBe(new ProductDto(id, name, 2.5m));
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        page.ShouldNotBeNull().Items.ShouldBe([new ProductDto(id, name, 2.5m)]);
    }

    [Fact]
    public async Task An_updated_product_is_read_back_then_deleted()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateAuthenticatedClient();
        var id = await client.CreateProductAsync($"Stylo-{Guid.NewGuid():N}", 2.5m, ct);

        var update = await client.PutAsJsonAsync($"/api/v1/products/{id}", new { name = "Stylo bleu", price = 3m }, ct);

        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var updated = await client.GetFromJsonAsync<ProductDto>($"/api/v1/products/{id}", ct);
        updated.ShouldBe(new ProductDto(id, "Stylo bleu", 3m));

        // Suppression logique, par l'Owner : la lecture qui précède a mis le produit en cache.
        var delete = await client.DeleteAsync($"/api/v1/products/{id}", ct);

        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/products/{id}", ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        page.ShouldNotBeNull().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_invalid_product_returns_a_400_naming_each_invalid_field()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/v1/products", new { name = "", price = -1m }, ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(ct);
        problem.ShouldNotBeNull().Errors.Keys.ShouldBe(["Name", "Price"], ignoreOrder: true);
    }

    [Fact]
    public async Task Pagination_page1_pageSize2_returns_correct_items_and_total()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateAuthenticatedClient();
        for (var i = 1; i <= 4; i++)
        {
            await client.CreateProductAsync($"ProdPag-{i:D2}-{Guid.NewGuid():N}", 1m, ct);
        }

        var page1 = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?page=1&pageSize=2", ct);
        page1.ShouldNotBeNull();
        page1.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(4);
        page1.TotalPages.ShouldBe(2);
        page1.Page.ShouldBe(1);
        page1.PageSize.ShouldBe(2);

        var page2 = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?page=2&pageSize=2", ct);
        page2.ShouldNotBeNull();
        page2.Items.Count.ShouldBe(2);
        page2.TotalCount.ShouldBe(4);
    }
}
