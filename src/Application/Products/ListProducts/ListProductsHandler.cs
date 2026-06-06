using Dapper;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Products.ListProducts;

public sealed class ListProductsHandler(IDbConnectionFactory factory, ITenantProvider tenant)
    : IQueryHandler<ListProductsQuery, IReadOnlyList<ProductDto>>
{
    public async ValueTask<IReadOnlyList<ProductDto>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT "Id", "Name", "Price"
            FROM "Products"
            WHERE "TenantId" = @TenantId AND "IsDeleted" = FALSE
            ORDER BY "Name"
            """,
            new { tenant.TenantId },
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<ProductDto>(command);
        return rows.ToList();
    }
}
