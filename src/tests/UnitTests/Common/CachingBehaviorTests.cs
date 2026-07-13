using ErrorOr;
using Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Saas.Application.Common.Behaviors;
using Saas.Application.Common.Interfaces;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Common;

public sealed class CachingBehaviorTests
{
    private static readonly Guid _tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid _tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // Provider tenant mutable : simule A puis B sans rebâtir le behavior.
    private sealed class FakeTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; set; } = _tenantA;
    }

    private static class FakeErrors
    {
        public static readonly Error NotFound = Error.NotFound("Fake.NotFound", "Introuvable.");
    }

    public sealed record FakeDto(int Value);

    // Query cacheable (opt-in via l'attribut).
    [CachedQuery(60, Tag = "widgets")]
    public sealed record FakeQuery(int Id) : IQuery<ErrorOr<FakeDto>>;

    // Query SANS attribut : le behavior doit être transparent.
    public sealed record UncachedQuery(int Id) : IQuery<ErrorOr<FakeDto>>;

    private static HybridCache NewCache()
    {
        var services = new ServiceCollection();
#pragma warning disable EXTEXP0018 // HybridCache stable en .NET 10
        services.AddHybridCache();
#pragma warning restore EXTEXP0018
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task Miss_puis_hit_ne_rappelle_pas_le_handler_et_renvoie_la_meme_valeur()
    {
        var ct = TestContext.Current.CancellationToken;
        var behavior = new CachingBehavior<FakeQuery, ErrorOr<FakeDto>>(NewCache(), new FakeTenantProvider());
        var calls = 0;

        ValueTask<ErrorOr<FakeDto>> Next(FakeQuery q, CancellationToken t)
        {
            calls++;
            return ValueTask.FromResult<ErrorOr<FakeDto>>(new FakeDto(42));
        }

        var query = new FakeQuery(1);
        var first = await behavior.Handle(query, Next, ct);
        var second = await behavior.Handle(query, Next, ct);

        calls.ShouldBe(1);
        first.Value.Value.ShouldBe(42);
        second.Value.Value.ShouldBe(42);
    }

    [Fact]
    public async Task Deux_tenants_ont_des_cles_separees()
    {
        var ct = TestContext.Current.CancellationToken;
        var tenant = new FakeTenantProvider();
        var behavior = new CachingBehavior<FakeQuery, ErrorOr<FakeDto>>(NewCache(), tenant);
        var calls = 0;

        ValueTask<ErrorOr<FakeDto>> Next(FakeQuery q, CancellationToken t)
        {
            calls++;
            return ValueTask.FromResult<ErrorOr<FakeDto>>(new FakeDto(calls));
        }

        var query = new FakeQuery(1);

        tenant.TenantId = _tenantA;
        await behavior.Handle(query, Next, ct);

        tenant.TenantId = _tenantB;
        await behavior.Handle(query, Next, ct);

        // Même message, tenants différents → 2 entrées de cache distinctes → 2 appels handler.
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Une_reponse_erreur_nest_jamais_cachee()
    {
        var ct = TestContext.Current.CancellationToken;
        var behavior = new CachingBehavior<FakeQuery, ErrorOr<FakeDto>>(NewCache(), new FakeTenantProvider());
        var calls = 0;

        ValueTask<ErrorOr<FakeDto>> Next(FakeQuery q, CancellationToken t)
        {
            calls++;
            return ValueTask.FromResult<ErrorOr<FakeDto>>(FakeErrors.NotFound);
        }

        var query = new FakeQuery(1);
        var first = await behavior.Handle(query, Next, ct);
        var second = await behavior.Handle(query, Next, ct);

        first.IsError.ShouldBeTrue();
        first.FirstError.Code.ShouldBe("Fake.NotFound");
        second.IsError.ShouldBeTrue();
        // L'erreur n'est pas cachée → le handler est rappelé.
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task RemoveByTagAsync_invalide_lentree_et_reexecute_le_handler()
    {
        var ct = TestContext.Current.CancellationToken;
        var cache = NewCache();
        var behavior = new CachingBehavior<FakeQuery, ErrorOr<FakeDto>>(cache, new FakeTenantProvider());
        var calls = 0;

        ValueTask<ErrorOr<FakeDto>> Next(FakeQuery q, CancellationToken t)
        {
            calls++;
            return ValueTask.FromResult<ErrorOr<FakeDto>>(new FakeDto(calls));
        }

        var query = new FakeQuery(1);
        await behavior.Handle(query, Next, ct);          // peuple le cache
        await behavior.Handle(query, Next, ct);          // hit
        calls.ShouldBe(1);

        await cache.RemoveByTagAsync($"t:{_tenantA}:widgets", ct);

        await behavior.Handle(query, Next, ct);          // invalidé → re-exécution
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Message_sans_attribut_est_transparent()
    {
        var ct = TestContext.Current.CancellationToken;
        var behavior = new CachingBehavior<UncachedQuery, ErrorOr<FakeDto>>(NewCache(), new FakeTenantProvider());
        var calls = 0;

        ValueTask<ErrorOr<FakeDto>> Next(UncachedQuery q, CancellationToken t)
        {
            calls++;
            return ValueTask.FromResult<ErrorOr<FakeDto>>(new FakeDto(calls));
        }

        var query = new UncachedQuery(1);
        await behavior.Handle(query, Next, ct);
        await behavior.Handle(query, Next, ct);

        // Aucun cache → handler appelé à chaque fois.
        calls.ShouldBe(2);
    }
}
