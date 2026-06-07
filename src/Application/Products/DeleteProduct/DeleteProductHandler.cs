using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Products;

namespace Saas.Application.Products.DeleteProduct;

public sealed class DeleteProductHandler(IProductRepository products, IUnitOfWork uow)
    : ICommandHandler<DeleteProductCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = await products.GetByIdAsync(ProductId.From(command.Id), cancellationToken)
            ?? throw new NotFoundException(nameof(Product), command.Id);

        products.Remove(product); // converti en soft-delete par l'intercepteur
        await uow.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
