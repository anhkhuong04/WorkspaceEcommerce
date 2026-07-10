using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Loyalty;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Loyalty;

internal sealed class CustomerLoyaltyAccountConfiguration : IEntityTypeConfiguration<CustomerLoyaltyAccount>
{
    public void Configure(EntityTypeBuilder<CustomerLoyaltyAccount> builder)
    {
        builder.ToTable("customer_loyalty_accounts", "loyalty");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(account => account.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(account => account.CurrentPoints)
            .HasColumnName("current_points")
            .IsRequired();

        builder.Property(account => account.TotalPointsEarned)
            .HasColumnName("total_points_earned")
            .IsRequired();

        builder.Property(account => account.CurrentTier)
            .HasColumnName("current_tier")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(account => account.TierUpdatedAt)
            .HasColumnName("tier_updated_at")
            .IsRequired();

        builder.Property<uint>("Version")
            .IsRowVersion();

        builder.HasIndex(account => account.CustomerId)
            .IsUnique()
            .HasDatabaseName("ux_customer_loyalty_accounts_customer_id");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(account => account.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(account => account.Transactions)
            .WithOne()
            .HasForeignKey(transaction => transaction.CustomerLoyaltyAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(account => account.Transactions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
