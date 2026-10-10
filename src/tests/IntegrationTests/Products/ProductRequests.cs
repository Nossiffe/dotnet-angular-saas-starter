using System.Net;
using System.Net.Http.Json;
using Saas.Api.Contracts;
using Shouldly;

namespace Saas.IntegrationTests.Products;

public static class ProductRequests
{
    /// <summary>Crée un produit et renvoie son identifiant, lu dans la réponse 201 dont le Location est vérifié.</summary>
    public static async Task<Guid> CreateProductAsync(
        this HttpClient client, string name, decimal price, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/products", new { name, price }, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<CreateProductResponse>(cancellationToken);
        created.ShouldNotBeNull();
        created.Id.ShouldNotBe(Guid.Empty);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/api/v1/products/{created.Id}");
        return created.Id;
    }
}
