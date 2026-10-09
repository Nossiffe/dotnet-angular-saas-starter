using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Saas.Infrastructure.Persistence.Outbox;

internal sealed class OutboxMessageConsumerConfiguration : IEntityTypeConfiguration<OutboxMessageConsumer>
{
    public void Configure(EntityTypeBuilder<OutboxMessageConsumer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("OutboxMessageConsumers");
        builder.HasKey(c => new { c.MessageId, c.Handler });
        builder.HasOne<OutboxMessage>().WithMany().HasForeignKey(c => c.MessageId).OnDelete(DeleteBehavior.Cascade);
    }
}
