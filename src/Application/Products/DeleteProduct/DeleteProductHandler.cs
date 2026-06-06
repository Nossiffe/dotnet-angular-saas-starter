using Mediator;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Common;

namespace Saas.Application.Products.DeleteProduct;

public sealed class DeleteProductHandler(IAppDbContext db) : ICommandHandler<DeleteProductCommand, Unit>
{
    public async ValueTask<Unit> Handle(DeleteProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Products.Product), command.Id);

        db.Products.Remove(product); // converti en soft-delete par l'intercepteur
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
