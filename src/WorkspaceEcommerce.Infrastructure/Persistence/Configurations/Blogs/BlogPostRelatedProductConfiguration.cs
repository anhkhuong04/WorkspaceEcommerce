using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Blogs;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Blogs;

internal sealed class BlogPostRelatedProductConfiguration : IEntityTypeConfiguration<BlogPostRelatedProduct>
{
    public void Configure(EntityTypeBuilder<BlogPostRelatedProduct> builder)
    {
        builder.ToTable("blog_post_related_products", "content");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.BlogPostId)
            .HasColumnName("blog_post_id")
            .IsRequired();

        builder.Property(x => x.ProductId)
            .HasColumnName("product_id")
            .IsRequired();

        builder.HasIndex(x => new { x.BlogPostId, x.ProductId })
            .IsUnique()
            .HasDatabaseName("ix_blog_post_related_products_blog_post_id_product_id");

        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(x => x.BlogPostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
