using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Customers;

internal sealed class CustomerTwoFactorChallengeConfiguration : IEntityTypeConfiguration<CustomerTwoFactorChallenge>
{
    public void Configure(EntityTypeBuilder<CustomerTwoFactorChallenge> builder)
    {
        builder.ToTable("two_factor_challenges", "customer");

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(challenge => challenge.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(challenge => challenge.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(challenge => challenge.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(challenge => challenge.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(challenge => challenge.ConsumedAt)
            .HasColumnName("consumed_at");

        builder.HasIndex(challenge => challenge.TokenHash)
            .IsUnique()
            .HasDatabaseName("ux_two_factor_challenges_token_hash");

        builder.HasIndex(challenge => new { challenge.CustomerId, challenge.ExpiresAt })
            .HasDatabaseName("ix_two_factor_challenges_customer_expiry");
    }
}
