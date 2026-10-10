using Dapper;
using ErrorOr;
using Mediator;
using Saas.Application.Common;
using Saas.Application.Common.Interfaces;

namespace Saas.Application.Products.ListProducts;

public sealed class ListProductsHandler(IDbConnectionFactory factory, ITenantProvider tenant)
    : IQueryHandler<ListProductsQuery, ErrorOr<PagedResult<ProductDto>>>
{
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;

    public async ValueTask<ErrorOr<PagedResult<ProductDto>>> Handle(ListProductsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, MinPageSize, MaxPageSize);
        var offset = (page - 1) * pageSize;

        await using var connection = await factory.OpenConnectionAsync(cancellationToken);

        // "Id" départage les produits de même nom : l'ordre reste le même d'une page à l'autre.
        var rowsCommand = new CommandDefinition(
            """
            SELECT "Id", "Name", "Price_Amount" AS "Price", COUNT(*) OVER() AS "TotalCount"
            FROM "Products"
            WHERE "TenantId" = @TenantId AND "IsDeleted" = FALSE
            ORDER BY "Name", "Id"
            LIMIT @PageSize OFFSET @Offset
            """,
            new { tenant.TenantId, PageSize = pageSize, Offset = offset },
            cancellationToken: cancellationToken);

        var rows = (await connection.QueryAsync<ProductRow>(rowsCommand)).ToList();

        int totalCount;
        List<ProductDto> items;

        if (rows.Count == 0)
        {
            // Une page vide n'a aucune ligne pour porter COUNT(*) OVER() : le total est compté à part.
            var countCommand = new CommandDefinition(
                """
                SELECT COUNT(*) FROM "Products"
                WHERE "TenantId" = @TenantId AND "IsDeleted" = FALSE
                """,
                new { tenant.TenantId },
                cancellationToken: cancellationToken);
            // COUNT(*) renvoie un bigint PostgreSQL (long).
            totalCount = (int)(await connection.ExecuteScalarAsync<long>(countCommand));
            items = [];
        }
        else
        {
            totalCount = (int)rows[0].TotalCount;
            items = rows.ConvertAll(r => new ProductDto(r.Id, r.Name, r.Price));
        }

        return new PagedResult<ProductDto>(items, page, pageSize, totalCount);
    }

    // Ligne lue par Dapper. TotalCount vient de COUNT(*) OVER(), un bigint PostgreSQL (long).
    internal sealed record ProductRow(Guid Id, string Name, decimal Price, long TotalCount);
}
