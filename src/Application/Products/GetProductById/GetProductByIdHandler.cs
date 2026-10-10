using Dapper;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Caching.Hybrid;
using Saas.Application.Common.Interfaces;
using Saas.Domain.Products;

namespace Saas.Application.Products.GetProductById;

// Lecture mise en cache 60 s (HybridCache), invalidée par UpdateProduct et DeleteProduct. Un produit
// absent est mis en cache comme absent : les identifiants sont des UUID v7, aucun produit ne
// peut apparaître plus tard sous un identifiant déjà demandé.
public sealed class GetProductByIdHandler(IDbConnectionFactory factory, ITenantProvider tenant, HybridCache cache)
    : IQueryHandler<GetProductByIdQuery, ErrorOr<ProductDto>>
{
    public async ValueTask<ErrorOr<ProductDto>> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        // Le tenant est lu ici, sur la requête HTTP : la fabrique du cache peut s'exécuter ailleurs.
        var tenantId = tenant.TenantId;
#pragma warning disable EXTEXP0018 // surcharge avec tags : stable en .NET 10, attribut Experimental résiduel
        var dto = await cache.GetOrCreateAsync(
            ProductCache.Key(tenantId, query.Id),
            token => LoadAsync(query.Id, tenantId, token),
            new HybridCacheEntryOptions { Expiration = ProductCache.Expiration },
            [ProductCache.Tag(tenantId)],
            cancellationToken);
#pragma warning restore EXTEXP0018

        return dto is null ? ProductErrors.NotFound : dto;
    }

    private async ValueTask<ProductDto?> LoadAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var connection = await factory.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProductDto>(new CommandDefinition(
            """
            SELECT "Id", "Name", "Price_Amount" AS "Price"
            FROM "Products"
            WHERE "Id" = @Id AND "TenantId" = @TenantId AND "IsDeleted" = FALSE
            """,
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }
}
