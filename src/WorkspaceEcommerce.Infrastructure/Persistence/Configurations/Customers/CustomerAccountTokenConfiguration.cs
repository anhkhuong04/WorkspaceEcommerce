using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerAccountTokenConfiguration : IEntityTypeConfiguration<CustomerAccountToken>
{
    public void Configure(EntityTypeBuilder<CustomerAccountToken> builder)
    {
        builder.ToTable("account_tokens", "customer");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(token => token.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(token => token.ConsumedAt).HasColumnName("consumed_at");

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_customer_account_tokens_token_hash");
        builder.HasIndex(token => new { token.CustomerId, token.Purpose, token.ExpiresAt })
            .HasDatabaseName("ix_customer_account_tokens_customer_purpose_expiry");
        builder.HasIndex(token => new { token.ExpiresAt, token.ConsumedAt })
            .HasDatabaseName("ix_customer_account_tokens_cleanup");

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(token => token.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
