using ErrorOr;
using Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Products;

namespace Saas.Application.Products.DeleteProduct;

public sealed class DeleteProductHandler(
    IProductRepository products,
    IUnitOfWork uow,
    ITenantProvider tenant,
    HybridCache cache)
    : ICommandHandler<DeleteProductCommand, ErrorOr<Deleted>>
{
    public async ValueTask<ErrorOr<Deleted>> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(ProductId.From(command.Id), cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }

        products.Remove(product); // converti en soft-delete par l'intercepteur
        await uow.SaveChangesAsync(cancellationToken);

#pragma warning disable EXTEXP0018 // RemoveByTagAsync : stable en .NET 10, attribut Experimental résiduel
        await cache.RemoveByTagAsync(ProductCache.Tag(tenant.TenantId), cancellationToken);
#pragma warning restore EXTEXP0018
        return Result.Deleted;
    }
}
