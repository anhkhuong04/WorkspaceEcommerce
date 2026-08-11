using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class ProductVariantWarrantyPlanConfiguration : IEntityTypeConfiguration<ProductVariantWarrantyPlan>
{
    public void Configure(EntityTypeBuilder<ProductVariantWarrantyPlan> builder)
    {
        builder.ToTable("product_variant_warranty_plans", "warranty");
        builder.HasKey(mapping => mapping.Id);
        builder.Property(mapping => mapping.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(mapping => mapping.ProductVariantId).HasColumnName("product_variant_id").IsRequired();
        builder.Property(mapping => mapping.WarrantyPlanId).HasColumnName("warranty_plan_id").IsRequired();
        builder.Property(mapping => mapping.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(mapping => mapping.EffectiveTo).HasColumnName("effective_to");
        builder.Property(mapping => mapping.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(mapping => new { mapping.ProductVariantId, mapping.EffectiveFrom })
            .IsUnique()
            .HasDatabaseName("ux_variant_warranty_plan_effective");
        builder.HasIndex(mapping => new { mapping.WarrantyPlanId, mapping.EffectiveFrom })
            .HasDatabaseName("ix_variant_warranty_plan_plan_effective");
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(mapping => mapping.ProductVariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarrantyPlan>().WithMany().HasForeignKey(mapping => mapping.WarrantyPlanId).OnDelete(DeleteBehavior.Restrict);
    }
}
