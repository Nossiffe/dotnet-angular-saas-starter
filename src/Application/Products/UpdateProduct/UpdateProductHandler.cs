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

        await cache.RemoveAsync($"product:{tenant.TenantId}:{command.Id}", cancellationToken);
        return Result.Updated;
    }
}
