using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerRefreshTokenFamilyConfiguration : IEntityTypeConfiguration<CustomerRefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<CustomerRefreshTokenFamily> builder)
    {
        builder.ToTable("refresh_token_families", "customer");
        builder.HasKey(family => family.Id);

        builder.Property(family => family.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(family => family.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(family => family.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(family => family.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(family => family.RevokedAt).HasColumnName("revoked_at");
        builder.Property(family => family.RevocationReason).HasColumnName("revocation_reason").HasMaxLength(128);

        builder.HasIndex(family => new { family.CustomerId, family.ExpiresAt })
            .HasDatabaseName("ix_customer_refresh_token_families_customer_expiry");
        builder.HasIndex(family => new { family.RevokedAt, family.ExpiresAt })
            .HasDatabaseName("ix_customer_refresh_token_families_cleanup");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(family => family.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
