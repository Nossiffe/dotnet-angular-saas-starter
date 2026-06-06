using System.Net;
using System.Net.Http.Json;
using Saas.Application.Products;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductsEndpointTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Create_then_get_list_update_delete()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        // Create
        var create = await client.PostAsJsonAsync("/products", new { name = "Stylo", price = 2.5m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        // List (Dapper)
        var list = await client.GetFromJsonAsync<List<ProductDto>>("/products", ct);
        list.ShouldNotBeNull();
        list.ShouldContain(p => string.Equals(p.Name, "Stylo", StringComparison.Ordinal));
        var id = list!.Single(p => string.Equals(p.Name, "Stylo", StringComparison.Ordinal)).Id;

        // Get by id
        var got = await client.GetFromJsonAsync<ProductDto>($"/products/{id}", ct);
        got!.Price.ShouldBe(2.5m);

        // Update
        var update = await client.PutAsJsonAsync($"/products/{id}", new { name = "Stylo bleu", price = 3m }, ct);
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Delete (soft)
        var delete = await client.DeleteAsync($"/products/{id}", ct);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Après soft-delete : absent de la liste
        var after = await client.GetFromJsonAsync<List<ProductDto>>("/products", ct);
        after!.ShouldNotContain(p => p.Id == id);
    }

    [Fact]
    public async Task Create_invalide_renvoie_400_ProblemDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/products", new { name = "", price = -1m }, ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await resp.Content.ReadAsStringAsync(ct);
        body.ShouldContain("errors");
    }
}
