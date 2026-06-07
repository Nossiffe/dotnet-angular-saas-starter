using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Saas.Domain.Tenancy;

namespace Saas.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMembershipConfiguration : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).HasMaxLength(50).IsRequired();
        builder.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();
        builder.HasOne<Saas.Domain.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Saas.Domain.Tenancy.Organization>()
            .WithMany()
            .HasForeignKey(m => m.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
