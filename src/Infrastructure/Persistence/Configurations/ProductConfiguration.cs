using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Domain.Products;

namespace Saas.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();

        builder.OwnsOne(p => p.Price, price =>
        {
            price.Property(m => m.Amount).HasColumnName("Price_Amount").HasPrecision(18, 2);
            price.Property(m => m.Currency).HasColumnName("Price_Currency").HasMaxLength(3);
        });
        builder.Navigation(p => p.Price).IsRequired();

        builder.HasIndex(p => p.TenantId);
        builder.Ignore(p => p.DomainEvents);

        // Concurrence optimiste via la colonne système Postgres xmin (aucune colonne ajoutée).
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
