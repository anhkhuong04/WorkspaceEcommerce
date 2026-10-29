using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class ShippingQuoteConfiguration : IEntityTypeConfiguration<ShippingQuote>
{
    public void Configure(EntityTypeBuilder<ShippingQuote> builder)
    {
        builder.ToTable("quotes", "shipping", table =>
        {
            table.HasCheckConstraint("ck_shipping_quote_values", "customer_charge >= 0 AND weight_grams > 0 AND revision > 0 AND expires_at_utc > created_at_utc");
            table.HasCheckConstraint("ck_shipping_quote_method", "method IN ('WorkspaceEC', 'MiniLogistics')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.SubjectHash).HasColumnName("subject_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.InputFingerprint).HasColumnName("input_fingerprint").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CustomerCharge).HasColumnName("customer_charge").HasPrecision(18, 0);
        builder.Property(x => x.WeightGrams).HasColumnName("weight_grams");
        builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RatePlanId).HasColumnName("rate_plan_id");
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.HasIndex(x => x.ExpiresAtUtc);
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasOne<ShippingRatePlan>().WithMany().HasForeignKey(x => x.RatePlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderShippingSelectionConfiguration : IEntityTypeConfiguration<OrderShippingSelection>
{
    public void Configure(EntityTypeBuilder<OrderShippingSelection> builder)
    {
        builder.ToTable("order_selections", "shipping", table =>
        {
            table.HasCheckConstraint("ck_shipping_selection_values", "customer_charge >= 0 AND weight_grams > 0");
            table.HasCheckConstraint("ck_shipping_selection_method", "method IN ('WorkspaceEC', 'MiniLogistics')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.OrderId).HasColumnName("order_id");
        builder.Property(x => x.QuoteId).HasColumnName("quote_id");
        builder.Property(x => x.RatePlanId).HasColumnName("rate_plan_id");
        builder.Property(x => x.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.CustomerCharge).HasColumnName("customer_charge").HasPrecision(18, 0);
        builder.Property(x => x.WeightGrams).HasColumnName("weight_grams");
        builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.OrderId).IsUnique();
        builder.HasIndex(x => x.QuoteId).IsUnique();
        builder.HasOne<Order>().WithOne().HasForeignKey<OrderShippingSelection>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShippingQuote>().WithOne().HasForeignKey<OrderShippingSelection>(x => x.QuoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ShippingRatePlan>().WithMany().HasForeignKey(x => x.RatePlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
