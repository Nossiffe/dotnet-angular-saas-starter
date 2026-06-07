using Mediator;
using Saas.Application.Common;

namespace Saas.Application.Products.ListProducts;

public sealed record ListProductsQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<ProductDto>>;
