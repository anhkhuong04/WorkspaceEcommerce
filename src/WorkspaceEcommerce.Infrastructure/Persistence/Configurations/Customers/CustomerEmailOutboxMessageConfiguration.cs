using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerEmailOutboxMessageConfiguration : IEntityTypeConfiguration<CustomerEmailOutboxMessage>
{
    public void Configure(EntityTypeBuilder<CustomerEmailOutboxMessage> builder)
    {
        builder.ToTable("email_outbox", "customer");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(message => message.RecipientEmail).HasColumnName("recipient_email").HasMaxLength(250).IsRequired();
        builder.Property(message => message.Subject).HasColumnName("subject").HasMaxLength(250).IsRequired();
        builder.Property(message => message.ProtectedPayload).HasColumnName("protected_payload").HasMaxLength(8192).IsRequired();
        builder.Property(message => message.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(message => message.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(message => message.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(message => message.SentAt).HasColumnName("sent_at");
        builder.Property(message => message.LastError).HasColumnName("last_error").HasMaxLength(2000);

        builder.HasIndex(message => new { message.SentAt, message.NextAttemptAt })
            .HasDatabaseName("ix_customer_email_outbox_due");
        builder.HasIndex(message => message.CreatedAt)
            .HasDatabaseName("ix_customer_email_outbox_cleanup");
    }
}
