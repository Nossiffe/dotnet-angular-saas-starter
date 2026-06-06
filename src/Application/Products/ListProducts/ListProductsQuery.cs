using Mediator;

namespace Saas.Application.Products.ListProducts;

public sealed record ListProductsQuery : IQuery<IReadOnlyList<ProductDto>>;
