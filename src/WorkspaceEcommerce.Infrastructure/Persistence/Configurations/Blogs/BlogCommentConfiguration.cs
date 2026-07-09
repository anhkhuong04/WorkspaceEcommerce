using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Infrastructure.Persistence.Configurations.Blogs;

internal sealed class BlogCommentConfiguration : IEntityTypeConfiguration<BlogComment>
{
    public void Configure(EntityTypeBuilder<BlogComment> builder)
    {
        builder.ToTable("blog_comments", "content");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.BlogPostId)
            .HasColumnName("blog_post_id")
            .IsRequired();

        builder.Property(x => x.AuthorName)
            .HasColumnName("author_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.AuthorEmail)
            .HasColumnName("author_email")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.IsApproved)
            .HasColumnName("is_approved")
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.BlogPostId)
            .HasDatabaseName("ix_blog_comments_blog_post_id");

        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(x => x.BlogPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
