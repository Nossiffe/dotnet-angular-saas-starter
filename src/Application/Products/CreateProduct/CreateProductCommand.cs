using ErrorOr;
using Mediator;

namespace Saas.Application.Products.CreateProduct;

public sealed record CreateProductCommand(string Name, decimal Price) : ICommand<ErrorOr<Guid>>;
