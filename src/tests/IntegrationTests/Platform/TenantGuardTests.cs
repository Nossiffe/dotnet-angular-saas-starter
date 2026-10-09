using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Saas.Domain.Common;
using Saas.Domain.Products;
using Saas.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Platform;

[Collection("api")]
public sealed class TenantGuardTests(PostgresApiFactory factory)
{
    [Fact]
    public void Synchronous_SaveChanges_sets_tenant_and_audit_fields()
    {
        var tenantId = Guid.CreateVersion7();
        using var scope = ScopeForTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = Product.Create("Sync", Money.From(10m));

        db.Add(product);
        db.SaveChanges();

        product.TenantId.ShouldBe(tenantId);
        product.CreatedAt.ShouldNotBe(default);
        product.CreatedBy.ShouldBe("sync-test");
    }

    [Fact]
    public async Task Changing_the_tenant_of_an_existing_entity_is_refused()
    {
        var ct = TestContext.Current.CancellationToken;
        using var scope = ScopeForTenant(Guid.CreateVersion7());
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = Product.Create("Moved", Money.From(10m));
        db.Add(product);
        await db.SaveChangesAsync(ct);

        db.Entry(product).Property(nameof(ITenantOwned.TenantId)).CurrentValue = Guid.CreateVersion7();

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
    }

    private IServiceScope ScopeForTenant(Guid tenantId)
    {
        var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant", tenantId.ToString()), new Claim("sub", "sync-test")], "Test")),
        };
        return scope;
    }
}
