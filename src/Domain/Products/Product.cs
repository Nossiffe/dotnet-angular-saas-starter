using Saas.Domain.Common;

namespace Saas.Domain.Products;

public sealed class Product : AggregateRoot<ProductId>, IAuditable, ISoftDeletable, ITenantOwned
{
    private Product()
    {
        // EF
    }

    public string Name { get; private set; } = null!;
    public Money Price { get; private set; } = null!;

    // ITenantOwned — discriminant tenant, renseigné par l'intercepteur.
    public Guid TenantId { get; set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static Product Create(string name, Money price)
    {
        ArgumentNullException.ThrowIfNull(price);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Le nom est requis.", nameof(name));
        }

        var product = new Product
        {
            Id = ProductId.From(Guid.CreateVersion7()),
            Name = name.Trim(),
            Price = price,
        };
        product.Raise(new ProductCreatedDomainEvent(product.Id));
        return product;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Le nom est requis.", nameof(name));
        }

        Name = name.Trim();
    }

    public void ChangePrice(Money price)
    {
        ArgumentNullException.ThrowIfNull(price);
        Price = price;
    }
}
