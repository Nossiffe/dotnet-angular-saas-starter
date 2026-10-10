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
        var clientA = factory.CreateAuthenticatedClient();
        var clientB = factory.CreateAuthenticatedClient();

        var id = await clientA.CreateProductAsync($"Secret-{Guid.NewGuid():N}", 1m, ct);

        var listB = await clientB.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        listB.ShouldNotBeNull().Items.ShouldBeEmpty();
        var listA = await clientA.GetFromJsonAsync<PagedResult<ProductDto>>("/api/v1/products", ct);
        listA.ShouldNotBeNull().Items.ShouldHaveSingleItem().Id.ShouldBe(id);
    }
}
