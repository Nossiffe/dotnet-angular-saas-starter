using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Architecture;

public sealed partial class SecurityConventionTests
{
    private sealed class FixedTenant : ITenantProvider
    {
        public Guid TenantId => Guid.Parse("00000000-0000-0000-0000-000000000001");
    }

    private static AppDbContext CreateContext()
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only").Options, new FixedTenant());

    [Fact]
    public void Every_tenant_owned_entity_has_the_TenantFilter()
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
    public void Every_soft_deletable_entity_has_the_SoftDeleteFilter()
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

    // Les lectures Dapper ne passent pas par les query filters EF : chaque requête doit filtrer
    // elle-même sur le tenant. Le test lit le code source, pas les requêtes exécutées. Tout raw string
    // literal de src/ qui nomme une table d'entité ITenantOwned doit contenir le filtre du tenant.
    // Une requête écrite autrement, en chaîne simple ou par concaténation, lui échappe : la
    // convention est d'écrire le SQL en raw string literal.
    [Fact]
    public void Every_raw_SQL_literal_naming_a_tenant_owned_table_filters_on_the_tenant()
    {
        using var context = CreateContext();
        var tenantTables = context.Model.GetEntityTypes()
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

    [Fact]
    public void TenantProvider_throws_without_a_tenant_claim()
    {
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext(),
        };
        var provider = new Saas.Infrastructure.Identity.TenantProvider(accessor);
        Should.Throw<InvalidOperationException>(() => _ = provider.TenantId);
    }

    [Fact]
    public void TenantProvider_throws_for_an_empty_tenant()
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("tenant", Guid.Empty.ToString())], "test"));
        var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context };
        var provider = new Saas.Infrastructure.Identity.TenantProvider(accessor);
        Should.Throw<InvalidOperationException>(() => _ = provider.TenantId);
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
