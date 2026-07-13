using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed class SecurityConventionTests
{
    private sealed class FixedTenant : ITenantProvider
    {
        public Guid TenantId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only").Options, new FixedTenant());

    [Fact]
    public void Toute_entite_ITenantOwned_porte_le_filtre_nomme_TenantFilter()
    {
        using var context = CreateContext();
        var violations = context.Model.GetEntityTypes()
            .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters().All(f => !string.Equals(f.Key, "TenantFilter", StringComparison.Ordinal)))
            .Select(e => e.ClrType.Name)
            .ToList();
        violations.ShouldBeEmpty(
            $"Entités ITenantOwned sans TenantFilter : {string.Join(", ", violations)}");
    }

    [Fact]
    public void Toute_entite_ISoftDeletable_porte_le_filtre_nomme_SoftDeleteFilter()
    {
        using var context = CreateContext();
        var violations = context.Model.GetEntityTypes()
            .Where(e => typeof(ISoftDeletable).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters().All(f => !string.Equals(f.Key, "SoftDeleteFilter", StringComparison.Ordinal)))
            .Select(e => e.ClrType.Name)
            .ToList();
        violations.ShouldBeEmpty(
            $"Entités ISoftDeletable sans SoftDeleteFilter : {string.Join(", ", violations)}");
    }

    [Fact]
    public void TenantProvider_leve_quand_le_claim_est_absent()
    {
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };
        var provider = new Saas.Infrastructure.Identity.TenantProvider(accessor);
        Should.Throw<InvalidOperationException>(() => _ = provider.TenantId);
    }

    [Fact]
    public void TenantProvider_leve_quand_le_claim_est_Guid_Empty()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("tenant", Guid.Empty.ToString())], "test"));
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context };
        var provider = new Saas.Infrastructure.Identity.TenantProvider(accessor);
        Should.Throw<InvalidOperationException>(() => _ = provider.TenantId);
    }
}
