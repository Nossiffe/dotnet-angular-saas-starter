using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(o => o.Slug).IsUnique();
        builder.Ignore(o => o.DomainEvents);

        // Concurrence optimiste via la colonne système Postgres xmin (aucune colonne ajoutée).
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        // Les memberships font partie de l'agrégat Organization (entités enfant, owned collection).
        builder.OwnsMany(o => o.Memberships, m =>
        {
            m.ToTable("OrganizationMemberships");
            m.WithOwner().HasForeignKey(x => x.OrganizationId);
            m.HasKey(x => x.Id);
            // L'Id est attribué par le domaine (OrganizationMembership.Create → Guid.CreateVersion7()).
            // Sans ceci, EF traite la clé comme ValueGeneratedOnAdd : ajouter un membre à une organisation
            // DÉJÀ chargée (ex. accept d'invitation) infère l'état Modified (PK non vide) au lieu de Added,
            // d'où une UPDATE sur une ligne inexistante → DbUpdateConcurrencyException (0 ligne affectée).
            m.Property(x => x.Id).ValueGeneratedNever();
            m.Property(x => x.Role).HasConversion<string>().HasMaxLength(50).IsRequired();
            m.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
            m.HasIndex(x => x.UserId);
        });
        builder.Navigation(o => o.Memberships)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_memberships");
    }
}
