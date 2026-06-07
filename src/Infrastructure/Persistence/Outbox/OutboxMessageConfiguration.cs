using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Saas.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Type).IsRequired();
        builder.Property(m => m.Content).HasColumnType("text").IsRequired();
        builder.Property(m => m.Error).HasColumnType("text");

        // Index pour le poll de l'OutboxProcessor (lignes non traitées, ordre d'occurrence).
        builder.HasIndex(m => m.ProcessedOn);
    }
}
