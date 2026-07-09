using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Reviews;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Reviews;

internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews", "catalog");

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(review => review.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.Property(review => review.CustomerId)
            .HasColumnName("customer_id")
            .IsRequired();

        builder.Property(review => review.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(review => review.Comment)
            .HasColumnName("comment")
            .HasColumnType("text");

        builder.Property(review => review.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(review => review.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(review => review.ProductId)
            .HasDatabaseName("ix_reviews_product_id");

        builder.HasIndex(review => review.CustomerId)
            .HasDatabaseName("ix_reviews_customer_id");

        // One customer can only review a product once
        builder.HasIndex(review => new { review.ProductId, review.CustomerId })
            .IsUnique()
            .HasDatabaseName("ux_reviews_product_customer");
    }
}
