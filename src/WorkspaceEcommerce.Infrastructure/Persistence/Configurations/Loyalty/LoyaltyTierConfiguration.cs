using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Loyalty;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Loyalty;

internal sealed class LoyaltyTierConfiguration : IEntityTypeConfiguration<LoyaltyTier>
{
    public void Configure(EntityTypeBuilder<LoyaltyTier> builder)
    {
        builder.ToTable("loyalty_tiers", "loyalty");

        builder.HasKey(tier => tier.Id);

        builder.Property(tier => tier.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(tier => tier.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tier => tier.MinTotalPointsEarned)
            .HasColumnName("min_total_points_earned")
            .IsRequired();

        builder.Property(tier => tier.DiscountPercent)
            .HasColumnName("discount_percent")
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(tier => tier.FreeShippingEnabled)
            .HasColumnName("free_shipping_enabled")
            .IsRequired();

        builder.HasIndex(tier => tier.Type)
            .IsUnique()
            .HasDatabaseName("ux_loyalty_tiers_type");

        builder.HasData(
            new LoyaltyTier(
                Guid.Parse("8d12c98b-10ce-4aac-8904-000000000001"),
                LoyaltyTierType.Bronze,
                minTotalPointsEarned: 0,
                discountPercent: 0m,
                freeShippingEnabled: false),
            new LoyaltyTier(
                Guid.Parse("8d12c98b-10ce-4aac-8904-000000000002"),
                LoyaltyTierType.Silver,
                minTotalPointsEarned: 500,
                discountPercent: 3m,
                freeShippingEnabled: false),
            new LoyaltyTier(
                Guid.Parse("8d12c98b-10ce-4aac-8904-000000000003"),
                LoyaltyTierType.Gold,
                minTotalPointsEarned: 2000,
                discountPercent: 5m,
                freeShippingEnabled: true),
            new LoyaltyTier(
                Guid.Parse("8d12c98b-10ce-4aac-8904-000000000004"),
                LoyaltyTierType.Platinum,
                minTotalPointsEarned: 5000,
                discountPercent: 10m,
                freeShippingEnabled: true));
    }
}
