using Mediator;
using Microsoft.Extensions.Logging;
using Saas.Domain.Products;

namespace Saas.Application.Products.ProductCreated;

public sealed partial class ProductCreatedHandler(ILogger<ProductCreatedHandler> logger)
    : INotificationHandler<ProductCreatedDomainEvent>
{
    public ValueTask Handle(ProductCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        LogProductCreated(logger, notification.ProductId.Value);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Produit créé : {ProductId}")]
    private static partial void LogProductCreated(ILogger logger, Guid productId);
}
