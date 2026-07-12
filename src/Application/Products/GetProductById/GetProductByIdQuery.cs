using ErrorOr;
using Mediator;

namespace Saas.Application.Products.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IQuery<ErrorOr<ProductDto>>;
