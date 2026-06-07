using Mediator;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;

namespace Saas.Application.Products.UpdateProduct;

public sealed class UpdateProductHandler(IAppDbContext db) : ICommandHandler<UpdateProductCommand, Unit>
{
    public async ValueTask<Unit> Handle(UpdateProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Products.Product), command.Id);

        product.Name = command.Name;
        product.Price = command.Price;
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
