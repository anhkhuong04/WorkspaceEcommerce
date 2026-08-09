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

        builder.Property(x => x.ModerationStatus)
            .HasColumnName("moderation_status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.ModeratedAt)
            .HasColumnName("moderated_at");

        builder.Property(x => x.ModeratedBy)
            .HasColumnName("moderated_by")
            .HasMaxLength(250);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(x => x.BlogPostId)
            .HasDatabaseName("ix_blog_comments_blog_post_id");

        builder.HasIndex(x => new { x.BlogPostId, x.ModerationStatus, x.CreatedAt })
            .HasDatabaseName("ix_blog_comments_post_moderation_created");

        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(x => x.BlogPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
