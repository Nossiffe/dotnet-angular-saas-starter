using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Products;

namespace Saas.Application.Products.CreateProduct;

public sealed class CreateProductHandler(IAppDbContext db) : ICommandHandler<CreateProductCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = new Product { Name = command.Name, Price = command.Price };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}
