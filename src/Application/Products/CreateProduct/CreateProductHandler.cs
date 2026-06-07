using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;
using Saas.Domain.Products;

namespace Saas.Application.Products.CreateProduct;

public sealed class CreateProductHandler(IProductRepository products, IUnitOfWork uow)
    : ICommandHandler<CreateProductCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = Product.Create(command.Name, Money.From(command.Price));
        products.Add(product);
        await uow.SaveChangesAsync(cancellationToken);
        return product.Id.Value;
    }
}
