using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

internal sealed class AdminBlogService(
    IAppDbContext dbContext,
    IValidator<CreateBlogPostRequest> createValidator,
    IValidator<UpdateBlogPostRequest> updateValidator) : IAdminBlogService
{
    public Task<Result<IReadOnlyCollection<AdminBlogPostDto>>> GetBlogPostsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var posts = dbContext.BlogPosts
            .OrderByDescending(p => p.CreatedAt)
            .ToList();

        var postIds = posts.Select(p => p.Id).ToList();

        var relatedProductsMap = dbContext.BlogPostRelatedProducts
            .Where(rp => postIds.Contains(rp.BlogPostId))
            .ToLookup(rp => rp.BlogPostId, rp => rp.ProductId);

        var dtos = posts
            .Select(p => ToDto(p, relatedProductsMap[p.Id].ToArray()))
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<AdminBlogPostDto>>.Success(dtos));
    }

    public Task<Result<AdminBlogPostDto>> GetBlogPostByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var post = dbContext.BlogPosts
            .FirstOrDefault(p => p.Id == id);

        if (post is null)
        {
            return Task.FromResult(Result<AdminBlogPostDto>.NotFound("Blog post was not found."));
        }

        var relatedProductIds = dbContext.BlogPostRelatedProducts
            .Where(rp => rp.BlogPostId == id)
            .Select(rp => rp.ProductId)
            .ToArray();

        return Task.FromResult(Result<AdminBlogPostDto>.Success(ToDto(post, relatedProductIds)));
    }

    public async Task<Result<AdminBlogPostDto>> CreateBlogPostAsync(
        CreateBlogPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminBlogPostDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var slugExists = dbContext.BlogPosts
            .Any(p => p.Slug == request.Slug.Trim().ToLower());

        if (slugExists)
        {
            return Result<AdminBlogPostDto>.Conflict("Blog post slug already exists.");
        }

        var blogPost = new BlogPost(
            Guid.NewGuid(),
            request.Title,
            request.Slug,
            request.Summary,
            request.Content,
            request.ImageUrl,
            request.IsPublished);

        dbContext.Add(blogPost);

        var validProductIds = new List<Guid>();
        if (request.RelatedProductIds is { Count: > 0 })
        {
            validProductIds = dbContext.Products
                .Where(p => request.RelatedProductIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToList();

            foreach (var productId in validProductIds)
            {
                dbContext.Add(new BlogPostRelatedProduct(Guid.NewGuid(), blogPost.Id, productId));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminBlogPostDto>.Success(ToDto(blogPost, validProductIds));
    }

    public async Task<Result<AdminBlogPostDto>> UpdateBlogPostAsync(
        Guid id,
        UpdateBlogPostRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminBlogPostDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var blogPost = dbContext.BlogPosts
            .FirstOrDefault(p => p.Id == id);

        if (blogPost is null)
        {
            return Result<AdminBlogPostDto>.NotFound("Blog post was not found.");
        }

        var slugExists = dbContext.BlogPosts
            .Any(p => p.Id != id && p.Slug == request.Slug.Trim().ToLower());

        if (slugExists)
        {
            return Result<AdminBlogPostDto>.Conflict("Blog post slug already exists.");
        }

        blogPost.UpdateDetails(
            request.Title,
            request.Slug,
            request.Summary,
            request.Content,
            request.ImageUrl);

        if (request.IsPublished)
        {
            blogPost.Publish();
        }
        else
        {
            blogPost.Unpublish();
        }

        dbContext.Update(blogPost);

        // Update related products
        var currentRelated = dbContext.BlogPostRelatedProducts
            .Where(rp => rp.BlogPostId == id)
            .ToList();

        foreach (var rp in currentRelated)
        {
            dbContext.Remove(rp);
        }

        var validProductIds = new List<Guid>();
        if (request.RelatedProductIds is { Count: > 0 })
        {
            validProductIds = dbContext.Products
                .Where(p => request.RelatedProductIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToList();

            foreach (var productId in validProductIds)
            {
                dbContext.Add(new BlogPostRelatedProduct(Guid.NewGuid(), id, productId));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminBlogPostDto>.Success(ToDto(blogPost, validProductIds));
    }

    public async Task<Result<AdminBlogPostDto>> DeleteBlogPostAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var blogPost = dbContext.BlogPosts
            .FirstOrDefault(p => p.Id == id);

        if (blogPost is null)
        {
            return Result<AdminBlogPostDto>.NotFound("Blog post was not found.");
        }

        var relatedProductIds = dbContext.BlogPostRelatedProducts
            .Where(rp => rp.BlogPostId == id)
            .Select(rp => rp.ProductId)
            .ToArray();

        var dto = ToDto(blogPost, relatedProductIds);

        dbContext.Remove(blogPost);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminBlogPostDto>.Success(dto);
    }

    public async Task<Result<AdminBlogPostDto>> TogglePublishStatusAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var blogPost = dbContext.BlogPosts
            .FirstOrDefault(p => p.Id == id);

        if (blogPost is null)
        {
            return Result<AdminBlogPostDto>.NotFound("Blog post was not found.");
        }

        if (blogPost.IsPublished)
        {
            blogPost.Unpublish();
        }
        else
        {
            blogPost.Publish();
        }

        dbContext.Update(blogPost);
        await dbContext.SaveChangesAsync(cancellationToken);

        var relatedProductIds = dbContext.BlogPostRelatedProducts
            .Where(rp => rp.BlogPostId == id)
            .Select(rp => rp.ProductId)
            .ToArray();

        return Result<AdminBlogPostDto>.Success(ToDto(blogPost, relatedProductIds));
    }

    public Task<Result<IReadOnlyCollection<BlogCommentDto>>> GetBlogPostCommentsAsync(
        Guid postId,
        BlogCommentModerationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var comments = dbContext.BlogComments
            .Where(c => c.BlogPostId == postId && (!status.HasValue || c.ModerationStatus == status.Value))
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => ToCommentDto(c))
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<BlogCommentDto>>.Success(comments));
    }

    public Task<Result<IReadOnlyCollection<BlogCommentDto>>> GetBlogCommentsAsync(
        BlogCommentModerationStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var comments = dbContext.BlogComments
            .Where(comment => !status.HasValue || comment.ModerationStatus == status.Value)
            .OrderByDescending(comment => comment.CreatedAt)
            .Select(ToCommentDto)
            .ToArray();
        return Task.FromResult(Result<IReadOnlyCollection<BlogCommentDto>>.Success(comments));
    }

    public Task<Result<BlogCommentDto>> ApproveCommentAsync(
        Guid commentId,
        string moderatorIdentity,
        CancellationToken cancellationToken = default) =>
        ModerateCommentAsync(commentId, moderatorIdentity, approve: true, cancellationToken);

    public Task<Result<BlogCommentDto>> RejectCommentAsync(
        Guid commentId,
        string moderatorIdentity,
        CancellationToken cancellationToken = default) =>
        ModerateCommentAsync(commentId, moderatorIdentity, approve: false, cancellationToken);

    public async Task<Result<BlogCommentDto>> DeleteCommentAsync(
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        var comment = dbContext.BlogComments
            .FirstOrDefault(c => c.Id == commentId);

        if (comment is null)
        {
            return Result<BlogCommentDto>.NotFound("Comment was not found.");
        }

        comment.Reject("legacy-delete");
        dbContext.Update(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<BlogCommentDto>.Success(ToCommentDto(comment));
    }

    private async Task<Result<BlogCommentDto>> ModerateCommentAsync(
        Guid commentId,
        string moderatorIdentity,
        bool approve,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(moderatorIdentity))
        {
            return Result<BlogCommentDto>.Unauthorized("Moderator identity is required.");
        }

        var comment = dbContext.BlogComments.FirstOrDefault(candidate => candidate.Id == commentId);
        if (comment is null)
        {
            return Result<BlogCommentDto>.NotFound("Comment was not found.");
        }

        if (approve)
        {
            comment.Approve(moderatorIdentity);
        }
        else
        {
            comment.Reject(moderatorIdentity);
        }

        dbContext.Update(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<BlogCommentDto>.Success(ToCommentDto(comment));
    }

    private static AdminBlogPostDto ToDto(BlogPost post, IReadOnlyCollection<Guid> relatedProductIds)
    {
        return new AdminBlogPostDto(
            post.Id,
            post.Title,
            post.Slug,
            post.Summary,
            post.Content,
            post.ImageUrl,
            post.IsPublished,
            post.PublishedAt,
            post.CreatedAt,
            post.UpdatedAt,
            relatedProductIds);
    }

    private static BlogCommentDto ToCommentDto(BlogComment comment)
    {
        return new BlogCommentDto(
            comment.Id,
            comment.BlogPostId,
            comment.AuthorName,
            comment.AuthorEmail,
            comment.Content,
            comment.ModerationStatus,
            comment.CreatedAt,
            comment.ModeratedAt,
            comment.ModeratedBy);
    }
}
