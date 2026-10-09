using ErrorOr;
using Microsoft.Extensions.Caching.Hybrid;
using NSubstitute;
using Saas.Application.Auth;
using Saas.Application.Auth.ResetPassword;
using Saas.Application.Common.Interfaces;
using Saas.Application.Products.DeleteProduct;
using Saas.Application.Products.UpdateProduct;
using Saas.Domain.Common;
using Saas.Domain.Products;
using Shouldly;
using Xunit;

namespace Saas.UnitTests.Application;

public sealed class AuthAndProductHandlerTests
{
    private static readonly Guid _tenantId = Guid.CreateVersion7();

    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly IRefreshTokenStore _refresh = Substitute.For<IRefreshTokenStore>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITenantProvider _tenant = Substitute.For<ITenantProvider>();
    private readonly HybridCache _cache = Substitute.For<HybridCache>();

    // Ordre des écritures : l'invalidation du cache doit suivre l'enregistrement.
    private readonly List<string> _writes = [];

    public AuthAndProductHandlerTests()
    {
        _tenant.TenantId.Returns(_tenantId);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _writes.Add("save");
            return Task.FromResult(1);
        });
#pragma warning disable CA2012 // configuration NSubstitute : le ValueTask n'est pas consommé ici
        _cache.RemoveByTagAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _writes.Add($"invalidate {call.ArgAt<string>(0)}");
            return ValueTask.CompletedTask;
        });
#pragma warning restore CA2012
    }

    // ── ResetPassword ────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_successful_reset_revokes_every_open_session()
    {
        var userId = Guid.CreateVersion7();
        _identity.ResetPasswordAsync(userId, "token", "N3w-password!", Arg.Any<CancellationToken>()).Returns(true);

        var result = await Reset(userId);

        result.IsError.ShouldBeFalse();
        await _refresh.Received(1).RevokeAllAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_invalid_reset_link_changes_no_session()
    {
        var userId = Guid.CreateVersion7();
        _identity.ResetPasswordAsync(userId, "token", "N3w-password!", Arg.Any<CancellationToken>()).Returns(false);

        var result = await Reset(userId);

        result.FirstError.ShouldBe(AuthErrors.InvalidResetLink);
        await _refresh.DidNotReceiveWithAnyArgs().RevokeAllAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── Update / Delete product ──────────────────────────────────────────────────

    [Fact]
    public async Task Updating_a_product_saves_it_then_invalidates_the_tenant_cache()
    {
        var product = Product.Create("Stylo", Money.From(2m));
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var result = await new UpdateProductHandler(_products, _uow, _tenant, _cache)
            .Handle(new UpdateProductCommand(product.Id.Value, "Crayon", 3m), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        product.Name.ShouldBe("Crayon");
        product.Price.ShouldBe(Money.From(3m));
        _writes.ShouldBe(["save", $"invalidate products:{_tenantId}"]);
    }

    [Fact]
    public async Task An_unknown_product_is_neither_saved_nor_invalidated()
    {
        var id = Guid.CreateVersion7();

        var update = await new UpdateProductHandler(_products, _uow, _tenant, _cache)
            .Handle(new UpdateProductCommand(id, "Crayon", 3m), TestContext.Current.CancellationToken);
        var delete = await new DeleteProductHandler(_products, _uow, _tenant, _cache)
            .Handle(new DeleteProductCommand(id), TestContext.Current.CancellationToken);

        update.FirstError.ShouldBe(ProductErrors.NotFound);
        delete.FirstError.ShouldBe(ProductErrors.NotFound);
        _writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_a_product_removes_it_then_invalidates_the_tenant_cache()
    {
        var product = Product.Create("Stylo", Money.From(2m));
        _products.GetByIdAsync(product.Id, Arg.Any<CancellationToken>()).Returns(product);

        var result = await new DeleteProductHandler(_products, _uow, _tenant, _cache)
            .Handle(new DeleteProductCommand(product.Id.Value), TestContext.Current.CancellationToken);

        result.IsError.ShouldBeFalse();
        _products.Received(1).Remove(product);
        _writes.ShouldBe(["save", $"invalidate products:{_tenantId}"]);
    }

    private ValueTask<ErrorOr<Success>> Reset(Guid userId)
        => new ResetPasswordHandler(_identity, _refresh)
            .Handle(new ResetPasswordCommand(userId, "token", "N3w-password!"), TestContext.Current.CancellationToken);
}
