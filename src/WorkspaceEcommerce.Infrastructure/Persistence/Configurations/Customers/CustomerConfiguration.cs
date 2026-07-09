using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers", "customer");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(customer => customer.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(customer => customer.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(50);

        builder.Property(customer => customer.Email)
            .HasColumnName("email")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(customer => customer.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500);

        builder.Property(customer => customer.GoogleId)
            .HasColumnName("google_id")
            .HasMaxLength(100);

        builder.Property(customer => customer.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(1000);

        builder.Property(customer => customer.IsEmailVerified)
            .HasColumnName("is_email_verified")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(customer => customer.RewardPoints)
            .HasColumnName("reward_points")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(customer => customer.TwoFactorEnabled)
            .HasColumnName("two_factor_enabled")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(customer => customer.TwoFactorSecret)
            .HasColumnName("two_factor_secret")
            .HasMaxLength(64);

        builder.Property(customer => customer.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(customer => customer.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(customer => customer.Email)
            .IsUnique()
            .HasDatabaseName("ux_customers_email");

        builder.HasIndex(customer => customer.PhoneNumber)
            .HasDatabaseName("ix_customers_phone_number");

        builder.HasIndex(customer => customer.GoogleId)
            .IsUnique()
            .HasFilter("google_id IS NOT NULL")
            .HasDatabaseName("ux_customers_google_id");
    }
}
