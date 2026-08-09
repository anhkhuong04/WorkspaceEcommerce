using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerRefreshTokenConfiguration : IEntityTypeConfiguration<CustomerRefreshToken>
{
    public void Configure(EntityTypeBuilder<CustomerRefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", "customer");
        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.FamilyId).HasColumnName("family_id").IsRequired();
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(token => token.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(token => token.UsedAt).HasColumnName("used_at").IsConcurrencyToken();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_customer_refresh_tokens_token_hash");
        builder.HasIndex(token => new { token.FamilyId, token.ExpiresAt })
            .HasDatabaseName("ix_customer_refresh_tokens_family_expiry");
        builder.HasIndex(token => new { token.ExpiresAt, token.UsedAt })
            .HasDatabaseName("ix_customer_refresh_tokens_cleanup");

        builder.HasOne<CustomerRefreshTokenFamily>()
            .WithMany()
            .HasForeignKey(token => token.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
