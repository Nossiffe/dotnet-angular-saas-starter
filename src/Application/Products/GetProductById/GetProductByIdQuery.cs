using ErrorOr;
using Mediator;
using Saas.Application.Common.Behaviors;

namespace Saas.Application.Products.GetProductById;

[CachedQuery(60, Tag = "products")]
public sealed record GetProductByIdQuery(Guid Id) : IQuery<ErrorOr<ProductDto>>;
