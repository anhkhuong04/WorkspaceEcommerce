using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("addresses", "customer");

        builder.HasKey(address => address.Id);

        builder.Property(address => address.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(address => address.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(address => address.Label)
            .HasColumnName("label")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(address => address.ContactName)
            .HasColumnName("contact_name")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(address => address.ContactPhone)
            .HasColumnName("contact_phone")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(address => address.Street)
            .HasColumnName("street")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(address => address.Ward)
            .HasColumnName("ward")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(address => address.Province)
            .HasColumnName("province")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(address => address.IsDefault)
            .HasColumnName("is_default")
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(address => address.CustomerId)
            .HasDatabaseName("ix_addresses_customer_id");
    }
}
