using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(i => i.Id);
        builder.Property(i => i.InviteeEmail).HasMaxLength(256).IsRequired();
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.Property(i => i.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(i => new { i.OrganizationId, i.Status });
        builder.Ignore(i => i.DomainEvents);

        // FK vers Organizations (CASCADE) — pas de FK vers AspNetUsers (comme OrganizationMemberships).
        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(i => i.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Concurrence optimiste via la colonne système Postgres xmin (aucune colonne ajoutée).
        builder.Property<uint>("Version")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
