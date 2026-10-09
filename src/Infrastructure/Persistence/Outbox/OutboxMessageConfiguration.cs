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

        // Messages à traiter, dans l'ordre où l'OutboxProcessor les lit ; les messages abandonnés
        // n'y figurent pas.
        builder.HasIndex(m => new { m.OccurredOn, m.Id })
            .HasDatabaseName("IX_OutboxMessages_Pending")
            .HasFilter(OutboxProcessor.PendingFilter);

        // Messages traités, pour la purge.
        builder.HasIndex(m => m.ProcessedOn);
    }
}
