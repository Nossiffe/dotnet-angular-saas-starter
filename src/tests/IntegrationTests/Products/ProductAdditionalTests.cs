using System.Net;
using System.Net.Http.Json;
using Saas.Application.Common;
using Saas.Application.Products;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class ProductAdditionalTests(PostgresApiFactory factory)
{
    private static HttpClient AuthedClient(PostgresApiFactory f, Guid? tenant = null, string tenantRole = "Owner")
    {
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.TenantHeader,
            (tenant ?? Guid.CreateVersion7()).ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantRoleHeader, tenantRole);
        return client;
    }

    [Fact]
    public async Task Reading_a_product_of_another_tenant_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var clientA = AuthedClient(factory, tenantA);
        var clientB = AuthedClient(factory, tenantB);

        var name = $"CrossTenant-{Guid.NewGuid():N}";
        var create = await clientA.PostAsJsonAsync("/api/v1/products", new { name, price = 9.99m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var page = await clientA.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=100", ct);
        page.ShouldNotBeNull();
        var productId = page.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        // Tenant B demande ce produit par ID → 404 (global query filter EF)
        var resp = await clientB.GetAsync($"/api/v1/products/{productId}", ct);
        resp.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Deleting_as_a_Member_returns_403()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = Guid.CreateVersion7();

        var ownerClient = AuthedClient(factory, tenant, "Owner");
        var name = $"NonOwnerDel-{Guid.NewGuid():N}";
        var create = await ownerClient.PostAsJsonAsync("/api/v1/products", new { name, price = 1m }, ct);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var page = await ownerClient.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=100", ct);
        var id = page!.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        // Un Member tente de supprimer → 403 (politique TenantAdmin)
        var memberClient = AuthedClient(factory, tenant, "Member");
        var del = await memberClient.DeleteAsync($"/api/v1/products/{id}", ct);
        del.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_deleted_product_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = Guid.CreateVersion7();
        var client = AuthedClient(factory, tenant, "Owner");

        var name = $"SoftDel-{Guid.NewGuid():N}";
        (await client.PostAsJsonAsync("/api/v1/products", new { name, price = 5m }, ct)).EnsureSuccessStatusCode();

        var page = await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products?pageSize=100", ct);
        var id = page!.Items.Single(p => string.Equals(p.Name, name, StringComparison.Ordinal)).Id;

        var before = await client.GetAsync($"/api/v1/products/{id}", ct);
        before.StatusCode.ShouldBe(HttpStatusCode.OK);

        var del = await client.DeleteAsync($"/api/v1/products/{id}", ct);
        del.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Après soft-delete, GET par ID → 404 (query filter exclut IsDeleted=true)
        var after = await client.GetAsync($"/api/v1/products/{id}", ct);
        after.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
