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

    // Tenant, audit et suppression logique : renseignés par l'intercepteur EF, jamais par le code
    // métier. Les setters privés n'existent que pour EF, d'où la suppression de S1144.
#pragma warning disable S1144
    // ITenantOwned
    public Guid TenantId { get; private set; }

    // IAuditable
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    // ISoftDeletable
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
#pragma warning restore S1144

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
