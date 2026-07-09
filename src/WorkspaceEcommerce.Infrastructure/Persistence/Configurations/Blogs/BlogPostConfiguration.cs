using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Blogs;

internal sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("blog_posts", "content");

        builder.HasKey(post => post.Id);

        builder.Property(post => post.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(post => post.Title)
            .HasColumnName("title")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(post => post.Slug)
            .HasColumnName("slug")
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(post => post.Summary)
            .HasColumnName("summary")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(post => post.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(post => post.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(1000);

        builder.Property(post => post.IsPublished)
            .HasColumnName("is_published")
            .IsRequired();

        builder.Property(post => post.PublishedAt)
            .HasColumnName("published_at");

        builder.Property(post => post.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(post => post.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(post => post.Slug)
            .IsUnique()
            .HasDatabaseName("ix_blog_posts_slug");

        builder.HasIndex(post => post.IsPublished)
            .HasDatabaseName("ix_blog_posts_is_published");
    }
}
