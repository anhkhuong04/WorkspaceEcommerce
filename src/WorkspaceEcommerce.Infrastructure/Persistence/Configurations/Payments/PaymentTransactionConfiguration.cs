using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Payments;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> builder)
    {
        builder.ToTable("payment_transactions", "payments");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(transaction => transaction.OrderId)
            .HasColumnName("order_id")
            .IsRequired();

        builder.Property(transaction => transaction.Provider)
            .HasColumnName("provider")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(transaction => transaction.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(transaction => transaction.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(transaction => transaction.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(transaction => transaction.TxnRef)
            .HasColumnName("txn_ref")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(transaction => transaction.GatewayTransactionNo)
            .HasColumnName("gateway_transaction_no")
            .HasMaxLength(100);

        builder.Property(transaction => transaction.GatewayResponseCode)
            .HasColumnName("gateway_response_code")
            .HasMaxLength(50);

        builder.Property(transaction => transaction.GatewayResponseMessage)
            .HasColumnName("gateway_response_message")
            .HasMaxLength(500);

        builder.Property(transaction => transaction.SecureHash)
            .HasColumnName("secure_hash")
            .HasMaxLength(512);

        builder.Property(transaction => transaction.RawResponse)
            .HasColumnName("raw_response")
            .HasColumnType("jsonb");

        builder.Property(transaction => transaction.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(transaction => transaction.ProcessedAt)
            .HasColumnName("processed_at");

        builder.HasIndex(transaction => transaction.TxnRef)
            .IsUnique()
            .HasDatabaseName("ux_payment_transactions_txn_ref");

        builder.HasIndex(transaction => transaction.OrderId)
            .HasDatabaseName("ix_payment_transactions_order_id");

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(transaction => transaction.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
