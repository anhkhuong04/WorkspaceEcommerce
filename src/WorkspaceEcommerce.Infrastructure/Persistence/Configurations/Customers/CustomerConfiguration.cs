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
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(customer => customer.Email)
            .HasColumnName("email")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(customer => customer.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

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
    }
}
