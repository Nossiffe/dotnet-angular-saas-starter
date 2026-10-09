using System.Net.Http.Json;
using Saas.Application.Common;
using Saas.Application.Products;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class TenantIsolationTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task A_tenant_does_not_see_the_products_of_another()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantA.ToString());
        var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantB.ToString());

        var name = $"Secret-{Guid.NewGuid():N}";
        (await clientA.PostAsJsonAsync("/api/v1/products", new { name, price = 1m }, ct)).EnsureSuccessStatusCode();

        var listB = await clientB.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        listB.ShouldNotBeNull();
        listB!.Items.ShouldNotContain(p => p.Name == name);

        var listA = await clientA.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        listA.ShouldNotBeNull();
        listA!.Items.ShouldContain(p => p.Name == name);
    }
}
