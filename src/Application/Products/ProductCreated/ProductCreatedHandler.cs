using Microsoft.Extensions.Logging;
using Saas.Application.Common.Events;
using Saas.Domain.Products;

namespace Saas.Application.Products.ProductCreated;

public sealed partial class ProductCreatedHandler(ILogger<ProductCreatedHandler> logger)
    : IDomainEventHandler<ProductCreatedDomainEvent>
{
    public ValueTask HandleAsync(ProductCreatedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        LogProductCreated(logger, domainEvent.ProductId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Produit créé : {ProductId}")]
    private static partial void LogProductCreated(ILogger logger, Guid productId);
}
