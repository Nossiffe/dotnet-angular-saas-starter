using ErrorOr;
using Mediator;

namespace Saas.Application.Products.UpdateProduct;

public sealed record UpdateProductCommand(Guid Id, string Name, decimal Price) : ICommand<ErrorOr<Updated>>;
