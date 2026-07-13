using ErrorOr;
using Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Products;

namespace Saas.Application.Products.UpdateProduct;

public sealed class UpdateProductHandler(
    IProductRepository products,
    IUnitOfWork uow,
    ITenantProvider tenant,
    HybridCache cache)
    : ICommandHandler<UpdateProductCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = await products.GetByIdAsync(ProductId.From(command.Id), cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }

        product.Rename(command.Name);
        product.ChangePrice(Money.From(command.Price));
        await uow.SaveChangesAsync(cancellationToken);

        // Invalidation par tag : couvre la lecture by-id ET les listes futures du tenant.
#pragma warning disable EXTEXP0018 // RemoveByTagAsync : stable en .NET 10, attribut Experimental résiduel
        await cache.RemoveByTagAsync($"t:{tenant.TenantId}:products", cancellationToken);
#pragma warning restore EXTEXP0018
        return Result.Updated;
    }
}
