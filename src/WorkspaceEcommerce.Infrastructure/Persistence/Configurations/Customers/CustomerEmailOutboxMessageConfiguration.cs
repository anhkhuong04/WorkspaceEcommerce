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
        builder.Property(message => message.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(message => message.LeaseOwner).HasColumnName("lease_owner").HasMaxLength(200);
        builder.Property(message => message.LeaseToken)
            .HasColumnName("lease_token")
            .IsConcurrencyToken();
        builder.Property(message => message.LeaseExpiresAt).HasColumnName("lease_expires_at");
        builder.Property(message => message.DeadLetteredAt).HasColumnName("dead_lettered_at");

        builder.HasIndex(message => new { message.SentAt, message.NextAttemptAt })
            .HasDatabaseName("ix_customer_email_outbox_due");
        builder.HasIndex(message => new
            {
                message.SentAt,
                message.Status,
                message.DeadLetteredAt,
                message.NextAttemptAt,
                message.LeaseExpiresAt,
                message.Id
            })
            .HasDatabaseName("ix_customer_email_outbox_claim");
        builder.HasIndex(message => message.CreatedAt)
            .HasDatabaseName("ix_customer_email_outbox_cleanup");
    }
}
