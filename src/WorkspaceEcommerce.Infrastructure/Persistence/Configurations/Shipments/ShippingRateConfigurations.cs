using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Shipments;

internal sealed class ShippingRatePlanConfiguration : IEntityTypeConfiguration<ShippingRatePlan>
{
    public void Configure(EntityTypeBuilder<ShippingRatePlan> builder)
    {
        builder.ToTable("rate_plans", "shipping", table =>
            table.HasCheckConstraint("ck_shipping_rate_plan_revision", "revision > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.CatalogVersion).HasColumnName("catalog_version").HasMaxLength(100).IsRequired();
        builder.Property(x => x.OriginProvinceCode).HasColumnName("origin_province_code").HasMaxLength(2).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(x => x.RetiredAtUtc).HasColumnName("retired_at_utc");
        builder.HasIndex(x => x.Status).IsUnique().HasFilter("status = 'Published'")
            .HasDatabaseName("ux_shipping_rate_plan_published");
        builder.HasIndex(x => new { x.CreatedAtUtc, x.Id }).HasDatabaseName("ix_shipping_rate_plan_created");
        builder.HasMany(x => x.Zones).WithOne().HasForeignKey(x => x.RatePlanId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Zones).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ShippingRateZoneConfiguration : IEntityTypeConfiguration<ShippingRateZone>
{
    public void Configure(EntityTypeBuilder<ShippingRateZone> builder)
    {
        builder.ToTable("rate_zones", "shipping", table =>
        {
            table.HasCheckConstraint("ck_shipping_zone_weight", "base_weight_grams > 0 AND step_weight_grams > 0 AND (maximum_weight_grams IS NULL OR maximum_weight_grams >= base_weight_grams)");
            table.HasCheckConstraint("ck_shipping_zone_fees", "base_fee >= 0 AND step_fee >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RatePlanId).HasColumnName("rate_plan_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(x => x.BaseWeightGrams).HasColumnName("base_weight_grams");
        builder.Property(x => x.StepWeightGrams).HasColumnName("step_weight_grams");
        builder.Property(x => x.MaximumWeightGrams).HasColumnName("maximum_weight_grams");
        builder.Property(x => x.BaseFee).HasColumnName("base_fee").HasPrecision(18, 0);
        builder.Property(x => x.StepFee).HasColumnName("step_fee").HasPrecision(18, 0);
        builder.HasAlternateKey(x => new { x.Id, x.RatePlanId });
        builder.HasIndex(x => new { x.RatePlanId, x.Code }).IsUnique();
        builder.HasMany(x => x.Provinces).WithOne().HasForeignKey(x => new { x.ZoneId, x.RatePlanId })
            .HasPrincipalKey(x => new { x.Id, x.RatePlanId }).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Provinces).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ShippingZoneProvinceConfiguration : IEntityTypeConfiguration<ShippingZoneProvince>
{
    public void Configure(EntityTypeBuilder<ShippingZoneProvince> builder)
    {
        builder.ToTable("zone_provinces", "shipping");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ZoneId).HasColumnName("zone_id");
        builder.Property(x => x.RatePlanId).HasColumnName("rate_plan_id");
        builder.Property(x => x.ProvinceCode).HasColumnName("province_code").HasMaxLength(2).IsRequired();
        builder.HasIndex(x => new { x.RatePlanId, x.ProvinceCode }).IsUnique()
            .HasDatabaseName("ux_shipping_zone_plan_province");
    }
}

internal sealed class ShippingRateAuditConfiguration : IEntityTypeConfiguration<ShippingRateAudit>
{
    public void Configure(EntityTypeBuilder<ShippingRateAudit> builder)
    {
        builder.ToTable("rate_audits", "shipping");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RatePlanId).HasColumnName("rate_plan_id");
        builder.Property(x => x.Revision).HasColumnName("revision");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Actor).HasColumnName("actor").HasMaxLength(256).IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ChangesJson).HasColumnName("changes_json").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnName("occurred_at_utc");
        builder.HasIndex(x => new { x.RatePlanId, x.Revision }).IsUnique();
        builder.HasOne<ShippingRatePlan>().WithMany().HasForeignKey(x => x.RatePlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
