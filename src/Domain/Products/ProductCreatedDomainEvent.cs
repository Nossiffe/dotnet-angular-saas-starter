using Saas.Domain.Common;

namespace Saas.Domain.Products;

public sealed record ProductCreatedDomainEvent(ProductId ProductId) : IDomainEvent;
