using Mediator;
using Microsoft.EntityFrameworkCore;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Products.GetProductById;

public sealed class GetProductByIdHandler(IAppDbContext db) : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    public async ValueTask<ProductDto?> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        return await db.Products
            .Where(p => p.Id == query.Id)
            .Select(p => new ProductDto(p.Id, p.Name, p.Price))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
