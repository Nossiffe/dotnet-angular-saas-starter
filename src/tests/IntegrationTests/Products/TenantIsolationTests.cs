using System.Net.Http.Json;
using Saas.Application.Products;
using Saas.IntegrationTests.Auth;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Products;

[Collection("api")]
public sealed class TenantIsolationTests(PostgresApiFactory factory)
{
    [Fact]
    public async Task Un_tenant_ne_voit_pas_les_produits_dun_autre()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();

        var clientA = factory.CreateClient();
        clientA.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantA.ToString());
        var clientB = factory.CreateClient();
        clientB.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenantB.ToString());

        var name = $"Secret-{Guid.NewGuid():N}";
        (await clientA.PostAsJsonAsync("/products", new { name, price = 1m }, ct)).EnsureSuccessStatusCode();

        var listB = await clientB.GetFromJsonAsync<List<ProductDto>>("/products", ct);
        listB.ShouldNotBeNull();
        listB!.ShouldNotContain(p => p.Name == name);

        var listA = await clientA.GetFromJsonAsync<List<ProductDto>>("/products", ct);
        listA.ShouldNotBeNull();
        listA!.ShouldContain(p => p.Name == name);
    }
}
