using Saas.Domain.Common;

namespace Saas.Domain.Products;

public sealed class Product : BaseEntity, IAuditable, ISoftDeletable, ITenantOwned
{
    public required string Name { get; set; }
    public decimal Price { get; set; }

    // ITenantOwned
    public Guid TenantId { get; set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
