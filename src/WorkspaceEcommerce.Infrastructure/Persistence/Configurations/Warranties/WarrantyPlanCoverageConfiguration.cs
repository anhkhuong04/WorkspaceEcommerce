using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class WarrantyPlanCoverageConfiguration : IEntityTypeConfiguration<WarrantyPlanCoverage>
{
    public void Configure(EntityTypeBuilder<WarrantyPlanCoverage> builder)
    {
        builder.ToTable("warranty_plan_coverages", "warranty");
        builder.HasKey(coverage => coverage.Id);
        builder.Property(coverage => coverage.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(coverage => coverage.WarrantyPlanId).HasColumnName("warranty_plan_id").IsRequired();
        builder.Property(coverage => coverage.ComponentCode).HasColumnName("component_code").HasMaxLength(50).IsRequired();
        builder.Property(coverage => coverage.DisplayName).HasColumnName("display_name").HasMaxLength(250).IsRequired();
        builder.Property(coverage => coverage.DurationMonths).HasColumnName("duration_months").IsRequired();
        builder.Property(coverage => coverage.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.HasIndex(coverage => new { coverage.WarrantyPlanId, coverage.ComponentCode })
            .IsUnique()
            .HasDatabaseName("ux_warranty_plan_coverages_plan_component");
    }
}
