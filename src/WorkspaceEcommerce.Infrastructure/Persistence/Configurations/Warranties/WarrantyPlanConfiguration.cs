using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class WarrantyPlanConfiguration : IEntityTypeConfiguration<WarrantyPlan>
{
    public void Configure(EntityTypeBuilder<WarrantyPlan> builder)
    {
        builder.ToTable("warranty_plans", "warranty");
        builder.HasKey(plan => plan.Id);
        builder.Property(plan => plan.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(plan => plan.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(plan => plan.Name).HasColumnName("name").HasMaxLength(250).IsRequired();
        builder.Property(plan => plan.ActivationWindowDays).HasColumnName("activation_window_days").IsRequired();
        builder.Property(plan => plan.TermsVersion).HasColumnName("terms_version").HasMaxLength(100).IsRequired();
        builder.Property(plan => plan.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(plan => plan.EffectiveTo).HasColumnName("effective_to");
        builder.Property(plan => plan.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(plan => plan.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(plan => plan.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(plan => plan.Code).IsUnique().HasDatabaseName("ux_warranty_plans_code");
        builder.HasIndex(plan => new { plan.IsActive, plan.EffectiveFrom, plan.Code })
            .IsDescending(false, true, false)
            .HasDatabaseName("ix_warranty_plans_active_effective_code");

        builder.HasMany(plan => plan.Coverages)
            .WithOne()
            .HasForeignKey(coverage => coverage.WarrantyPlanId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(plan => plan.Coverages).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
