using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Warranties;

internal sealed class WarrantyEntitlementConfiguration : IEntityTypeConfiguration<WarrantyEntitlement>
{
    public void Configure(EntityTypeBuilder<WarrantyEntitlement> builder)
    {
        builder.ToTable("warranty_entitlements", "warranty");
        builder.HasKey(entitlement => entitlement.Id);
        builder.Property(entitlement => entitlement.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(entitlement => entitlement.SerializedProductUnitId).HasColumnName("serialized_product_unit_id").IsRequired();
        builder.Property(entitlement => entitlement.WarrantyPlanId).HasColumnName("warranty_plan_id").IsRequired();
        builder.Property(entitlement => entitlement.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(entitlement => entitlement.OrderItemId).HasColumnName("order_item_id").IsRequired();
        builder.Property(entitlement => entitlement.CustomerId).HasColumnName("customer_id");
        builder.Property(entitlement => entitlement.PurchasedAt).HasColumnName("purchased_at");
        builder.Property(entitlement => entitlement.EligibleAt).HasColumnName("eligible_at");
        builder.Property(entitlement => entitlement.ActivationDeadline).HasColumnName("activation_deadline");
        builder.Property(entitlement => entitlement.ActivatedAt).HasColumnName("activated_at");
        builder.Property(entitlement => entitlement.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(entitlement => entitlement.ActivationSource).HasColumnName("activation_source").HasConversion<string>().HasMaxLength(30);
        builder.Property(entitlement => entitlement.AcceptedTermsVersion).HasColumnName("accepted_terms_version").HasMaxLength(100);
        builder.Property(entitlement => entitlement.ReplacementSerializedProductUnitId).HasColumnName("replacement_serialized_product_unit_id");
        builder.Property(entitlement => entitlement.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(entitlement => entitlement.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(entitlement => entitlement.SerializedProductUnitId).IsUnique().HasDatabaseName("ux_warranty_entitlements_unit_id");
        builder.HasIndex(entitlement => new { entitlement.CustomerId, entitlement.ActivatedAt, entitlement.Id })
            .IsDescending(false, true, false)
            .HasDatabaseName("ix_warranty_entitlements_customer_activated_id");
        builder.HasIndex(entitlement => entitlement.OrderId).HasDatabaseName("ix_warranty_entitlements_order_id");
        builder.HasIndex(entitlement => entitlement.OrderItemId).HasDatabaseName("ix_warranty_entitlements_order_item_id");
        builder.HasIndex(entitlement => new { entitlement.Status, entitlement.ActivationDeadline }).HasDatabaseName("ix_warranty_entitlements_status_deadline");
        builder.HasIndex(entitlement => entitlement.ReplacementSerializedProductUnitId).HasDatabaseName("ix_warranty_entitlements_replacement_unit_id");
        builder.HasOne<SerializedProductUnit>().WithMany().HasForeignKey(entitlement => entitlement.SerializedProductUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WarrantyPlan>().WithMany().HasForeignKey(entitlement => entitlement.WarrantyPlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Order>().WithMany().HasForeignKey(entitlement => entitlement.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OrderItem>().WithMany().HasForeignKey(entitlement => entitlement.OrderItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Customer>().WithMany().HasForeignKey(entitlement => entitlement.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(entitlement => entitlement.CoverageSnapshots).WithOne().HasForeignKey(snapshot => snapshot.WarrantyEntitlementId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(entitlement => entitlement.CoverageSnapshots).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
