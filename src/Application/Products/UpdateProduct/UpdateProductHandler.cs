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
    : ICommandHandler<UpdateProductCommand, Unit>
{
    public async ValueTask<Unit> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = await products.GetByIdAsync(ProductId.From(command.Id), cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.Id);

        product.Rename(command.Name);
        product.ChangePrice(Money.From(command.Price));
        await uow.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync($"product:{tenant.TenantId}:{command.Id}", cancellationToken);
        return Unit.Value;
    }
}
