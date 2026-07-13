using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Products;

namespace Saas.Application.Products.GetProductById;

public sealed class GetProductByIdHandler(IDbConnectionFactory factory, ITenantProvider tenant)
    : IQueryHandler<GetProductByIdQuery, ErrorOr<ProductDto>>
{
    public async ValueTask<ErrorOr<ProductDto>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Le cache est géré en amont par le CachingBehavior ([CachedQuery] sur la query) :
        // clé + tags scopés au tenant, invalidation par tag sur write. Ici, uniquement le SQL.
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT "Id", "Name", "Price_Amount" AS "Price"
            FROM "Products"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "IsDeleted" = FALSE
            """,
            new { query.Id, TenantId = tenant.TenantId },
            cancellationToken: cancellationToken);

        var dto = await connection.QuerySingleOrDefaultAsync<ProductDto>(command);

        return dto is null ? (ErrorOr<ProductDto>)ProductErrors.NotFound : dto;
    }
}
