using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

internal sealed class StorefrontBlogService(
    IAppDbContext dbContext,
    IValidator<CreateCommentRequest> commentValidator) : IStorefrontBlogService
{
    public Task<Result<IReadOnlyCollection<StorefrontBlogPostDto>>> GetPublishedBlogPostsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var posts = dbContext.BlogPosts
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .ToList();

        var dtos = posts
            .Select(p => new StorefrontBlogPostDto(
                p.Id,
                p.Title,
                p.Slug,
                p.Summary,
                p.Content,
                p.ImageUrl,
                p.PublishedAt,
                Array.Empty<StorefrontProductListItemDto>(),
                Array.Empty<BlogCommentDto>()))
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<StorefrontBlogPostDto>>.Success(dtos));
    }

    public Task<Result<StorefrontBlogPostDto>> GetBlogPostBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var post = dbContext.BlogPosts
            .FirstOrDefault(p => p.IsPublished && p.Slug == normalizedSlug);

        if (post is null)
        {
            return Task.FromResult(Result<StorefrontBlogPostDto>.NotFound("Blog post was not found."));
        }

        var relatedProducts = GetRelatedProducts(post.Id);

        var comments = dbContext.BlogComments
            .Where(c => c.BlogPostId == post.Id && c.IsApproved)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => ToCommentDto(c))
            .ToList();

        var dto = new StorefrontBlogPostDto(
            post.Id,
            post.Title,
            post.Slug,
            post.Summary,
            post.Content,
            post.ImageUrl,
            post.PublishedAt,
            relatedProducts,
            comments);

        return Task.FromResult(Result<StorefrontBlogPostDto>.Success(dto));
    }

    public async Task<Result<BlogCommentDto>> SubmitCommentAsync(
        string slug,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await commentValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<BlogCommentDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var post = dbContext.BlogPosts
            .FirstOrDefault(p => p.IsPublished && p.Slug == normalizedSlug);

        if (post is null)
        {
            return Result<BlogCommentDto>.NotFound("Blog post was not found.");
        }

        var comment = new BlogComment(
            Guid.NewGuid(),
            post.Id,
            request.AuthorName,
            request.AuthorEmail,
            request.Content,
            isApproved: true); // Auto-approve for demo simplicity

        dbContext.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<BlogCommentDto>.Success(ToCommentDto(comment));
    }

    private IReadOnlyCollection<StorefrontProductListItemDto> GetRelatedProducts(Guid postId)
    {
        var productIds = dbContext.BlogPostRelatedProducts
            .Where(rp => rp.BlogPostId == postId)
            .Select(rp => rp.ProductId)
            .ToList();

        if (productIds.Count == 0)
        {
            return Array.Empty<StorefrontProductListItemDto>();
        }

        var products = dbContext.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToList();

        var categories = dbContext.Categories
            .Where(c => c.IsActive)
            .ToDictionary(c => c.Id);

        var variants = dbContext.ProductVariants
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .ToLookup(v => v.ProductId);

        var images = dbContext.ProductImages
            .Where(i => productIds.Contains(i.ProductId))
            .ToLookup(i => i.ProductId);

        var list = new List<StorefrontProductListItemDto>();
        foreach (var product in products)
        {
            categories.TryGetValue(product.CategoryId, out var category);
            var categoryName = category?.Name ?? "Unknown";

            var activeVariants = variants[product.Id].ToArray();
            decimal? minPrice = activeVariants.Length == 0 ? null : activeVariants.Min(v => v.Price);
            decimal? compareAtPrice = activeVariants.Length == 0 ? null : activeVariants.Where(v => v.CompareAtPrice != null).Max(v => v.CompareAtPrice);

            var primaryImage = images[product.Id]
                .OrderBy(img => img.SortOrder)
                .ThenBy(img => img.ImageUrl)
                .FirstOrDefault();

            list.Add(new StorefrontProductListItemDto(
                product.Id,
                product.CategoryId,
                categoryName,
                product.Name,
                product.Slug,
                product.Description,
                product.IsFeatured,
                minPrice,
                compareAtPrice,
                activeVariants.Any(v => v.StockQuantity > 0),
                primaryImage?.ImageUrl));
        }

        return list;
    }

    private static BlogCommentDto ToCommentDto(BlogComment comment)
    {
        return new BlogCommentDto(
            comment.Id,
            comment.BlogPostId,
            comment.AuthorName,
            comment.AuthorEmail,
            comment.Content,
            comment.IsApproved,
            comment.CreatedAt);
    }
}
