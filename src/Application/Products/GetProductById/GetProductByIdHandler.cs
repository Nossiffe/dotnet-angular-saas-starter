using Dapper;
using Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Products.GetProductById;

public sealed class GetProductByIdHandler(IDbConnectionFactory factory, ITenantProvider tenant, HybridCache cache)
    : IQueryHandler<GetProductByIdQuery, ProductDto?>
{
    public async ValueTask<ProductDto?> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var key = $"product:{tenant.TenantId}:{query.Id}";

        var tenantId = tenant.TenantId;
        var dto = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                await using var connection = await factory.OpenConnectionAsync(token);
                var command = new CommandDefinition(
                    """
                    SELECT "Id", "Name", "Price_Amount" AS "Price"
                    FROM "Products"
                    WHERE "Id" = @Id AND "TenantId" = @TenantId AND "IsDeleted" = FALSE
                    """,
                    new { query.Id, TenantId = tenantId },
                    cancellationToken: token);

                return await connection.QuerySingleOrDefaultAsync<ProductDto>(command);
            },
            cancellationToken: cancellationToken);

        // On ne garde pas un "miss" en cache : sinon un produit créé juste après resterait masqué
        // jusqu'à expiration. L'invalidation sur write reste l'autre garde-fou.
        if (dto is null)
        {
            await cache.RemoveAsync(key, cancellationToken);
        }

        return dto;
    }
}
