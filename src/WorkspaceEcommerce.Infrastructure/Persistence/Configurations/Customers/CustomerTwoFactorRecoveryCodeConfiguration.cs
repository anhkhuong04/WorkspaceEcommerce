using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerTwoFactorRecoveryCodeConfiguration : IEntityTypeConfiguration<CustomerTwoFactorRecoveryCode>
{
    public void Configure(EntityTypeBuilder<CustomerTwoFactorRecoveryCode> builder)
    {
        builder.ToTable("two_factor_recovery_codes", "customer");

        builder.HasKey(code => code.Id);

        builder.Property(code => code.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(code => code.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(code => code.CodeHash)
            .HasColumnName("code_hash")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(code => code.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(code => code.UsedAt)
            .HasColumnName("used_at")
            .IsConcurrencyToken();

        builder.HasIndex(code => new { code.CustomerId, code.UsedAt })
            .HasDatabaseName("ix_two_factor_recovery_codes_customer_used");
    }
}
