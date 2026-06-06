using Mediator;

namespace Saas.Application.Products.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : ICommand;
