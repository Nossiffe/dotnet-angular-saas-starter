using Saas.Domain.Common;

namespace Saas.Domain.Tenancy;

// L'organisation EST le tenant. Non multi-tenant elle-même (cross-tenant).
public sealed class Organization : BaseEntity, IAuditable
{
    public required string Name { get; set; }
    public required string Slug { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
