using Dapper;
using Mediator;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Products.GetProductById;

public sealed class GetProductByIdHandler(IDbConnectionFactory factory, ITenantProvider tenant)
    : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    public async ValueTask<ProductDto?> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT "Id", "Name", "Price_Amount" AS "Price"
            FROM "Products"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "IsDeleted" = FALSE
            """,
            new { query.Id, tenant.TenantId },
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<ProductDto>(command);
    }
}
