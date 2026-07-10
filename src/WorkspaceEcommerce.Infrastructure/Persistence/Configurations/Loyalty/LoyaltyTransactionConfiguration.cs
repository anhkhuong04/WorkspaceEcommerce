using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Loyalty;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Loyalty;

internal sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("loyalty_transactions", "loyalty");

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(transaction => transaction.CustomerLoyaltyAccountId)
            .HasColumnName("customer_loyalty_account_id")
            .IsRequired();

        builder.Property(transaction => transaction.OrderId)
            .HasColumnName("order_id");

        builder.Property(transaction => transaction.VoucherId)
            .HasColumnName("voucher_id");

        builder.Property(transaction => transaction.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(transaction => transaction.Points)
            .HasColumnName("points")
            .IsRequired();

        builder.Property(transaction => transaction.BalanceAfter)
            .HasColumnName("balance_after")
            .IsRequired();

        builder.Property(transaction => transaction.Description)
            .HasColumnName("description")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(transaction => transaction.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(transaction => transaction.CustomerLoyaltyAccountId)
            .HasDatabaseName("ix_loyalty_transactions_account_id");

        builder.HasIndex(transaction => transaction.OrderId)
            .IsUnique()
            .HasFilter("\"type\" = 'Earn' AND order_id IS NOT NULL")
            .HasDatabaseName("ux_loyalty_transactions_earn_order");

        builder.HasIndex(transaction => transaction.VoucherId)
            .HasDatabaseName("ix_loyalty_transactions_voucher_id");

        builder.HasIndex(transaction => transaction.CreatedAt)
            .HasDatabaseName("ix_loyalty_transactions_created_at");

        builder.HasOne<CustomerLoyaltyAccount>()
            .WithMany(account => account.Transactions)
            .HasForeignKey(transaction => transaction.CustomerLoyaltyAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(transaction => transaction.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(transaction => transaction.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
