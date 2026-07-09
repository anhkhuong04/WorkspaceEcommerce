using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerLoginHistoryConfiguration : IEntityTypeConfiguration<CustomerLoginHistory>
{
    public void Configure(EntityTypeBuilder<CustomerLoginHistory> builder)
    {
        builder.ToTable("login_history", "customer");

        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(history => history.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(history => history.LoginTime)
            .HasColumnName("login_time")
            .IsRequired();

        builder.Property(history => history.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(history => history.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(history => history.Success)
            .HasColumnName("success")
            .IsRequired();

        builder.HasIndex(history => history.CustomerId)
            .HasDatabaseName("ix_login_history_customer_id");

        builder.HasIndex(history => history.LoginTime)
            .HasDatabaseName("ix_login_history_login_time");
    }
}
