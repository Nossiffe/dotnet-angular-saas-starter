using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Saas.Domain.Common;
using Saas.Domain.Products;
using Saas.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Saas.IntegrationTests.Platform;

[Collection("api")]
public sealed partial class TenantGuardTests(PostgresApiFactory factory)
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

    // Les lectures Dapper ne passent pas par les query filters EF : chaque requête SQL qui lit une
    // table d'entité ITenantOwned doit filtrer elle-même sur "TenantId" = @TenantId.
    [Fact]
    public void Every_SQL_query_on_a_tenant_owned_table_filters_on_the_tenant()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;
        var tenantTables = model.GetEntityTypes()
            .Where(t => typeof(ITenantOwned).IsAssignableFrom(t.ClrType))
            .Select(t => t.GetTableName()!)
            .ToList();
        tenantTables.ShouldNotBeEmpty();

        var queries = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(f => RawStringLiteral().Matches(File.ReadAllText(f)).Select(m => (File: f, Sql: m.Groups["sql"].Value)))
            .Where(q => tenantTables.Exists(table => q.Sql.Contains($"\"{table}\"", StringComparison.Ordinal)))
            .ToList();

        queries.ShouldNotBeEmpty();
        queries.Where(q => !q.Sql.Contains("\"TenantId\" = @TenantId", StringComparison.Ordinal))
            .Select(q => Path.GetFileName(q.File))
            .ShouldBeEmpty();
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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Saas.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Racine du dépôt introuvable (Saas.slnx).");
    }

    [GeneratedRegex("\"\"\"(?<sql>.*?)\"\"\"", RegexOptions.Singleline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RawStringLiteral();
}
