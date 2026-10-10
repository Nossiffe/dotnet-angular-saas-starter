using System.Net;
using System.Text.Json;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductAccessTests(PostgresApiFactory factory)
{
    // L'autre organisation connaît l'identifiant : 404, comme pour un produit inexistant (filtre de tenant EF).
    [Fact]
    public async Task Reading_a_product_of_another_tenant_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await factory.CreateAuthenticatedClient()
            .CreateProductAsync($"CrossTenant-{Guid.NewGuid():N}", 9.99m, ct);

        var response = await factory.CreateAuthenticatedClient().GetAsync($"/api/v1/products/{id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("Product.NotFound");
        problem.RootElement.GetProperty("errors").GetProperty("Product.NotFound")[0].GetString()
            .ShouldBe("Produit introuvable.");
    }

    // Supprimer un produit demande la politique TenantAdmin (Owner ou Admin).
    [Fact]
    public async Task Deleting_as_a_Member_returns_403_and_keeps_the_product()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = Guid.CreateVersion7();
        var owner = factory.CreateAuthenticatedClient(tenant);
        var id = await owner.CreateProductAsync($"NonOwnerDel-{Guid.NewGuid():N}", 1m, ct);

        var response = await factory.CreateAuthenticatedClient(tenant, "Member").DeleteAsync($"/api/v1/products/{id}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await owner.GetAsync($"/api/v1/products/{id}", ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
