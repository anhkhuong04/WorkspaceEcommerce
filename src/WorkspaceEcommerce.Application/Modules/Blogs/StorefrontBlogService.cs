using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Modules.Catalog.Storefront;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Modules.Blogs;

internal sealed class StorefrontBlogService(
    IAppDbContext dbContext,
    ICurrentLanguageProvider languageProvider,
    IValidator<CreateCommentRequest> commentValidator) : IStorefrontBlogService
{
    private const int MaxBlogPosts = 100;
    private const int MaxBlogComments = 100;
    private const int MaxRelatedProducts = 100;

    public async Task<Result<IReadOnlyCollection<StorefrontBlogPostDto>>> GetPublishedBlogPostsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dtos = await dbContext.BlogPosts
            .AsNoTrackingIfEf()
            .Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAt)
            .ThenByDescending(p => p.Id)
            .Take(MaxBlogPosts)
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
            .ToArrayAsyncSafe(cancellationToken);

        return Result<IReadOnlyCollection<StorefrontBlogPostDto>>.Success(dtos);
    }

    public async Task<Result<StorefrontBlogPostDto>> GetBlogPostBySlugAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var post = await dbContext.BlogPosts
            .AsNoTrackingIfEf()
            .Where(p => p.IsPublished && p.Slug == normalizedSlug)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (post is null)
        {
            return Result<StorefrontBlogPostDto>.NotFound("Blog post was not found.");
        }

        var relatedProducts = await GetRelatedProductsAsync(post.Id, cancellationToken);

        var comments = await dbContext.BlogComments
            .AsNoTrackingIfEf()
            .Where(c => c.BlogPostId == post.Id && c.ModerationStatus == BlogCommentModerationStatus.Approved)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Take(MaxBlogComments)
            .Select(c => ToCommentDto(c))
            .ToArrayAsyncSafe(cancellationToken);

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

        return Result<StorefrontBlogPostDto>.Success(dto);
    }

    public async Task<Result<CommentSubmissionAcknowledgement>> SubmitCommentAsync(
        string slug,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await commentValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CommentSubmissionAcknowledgement>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var post = await dbContext.BlogPosts
            .Where(p => p.IsPublished && p.Slug == normalizedSlug)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        if (post is null)
        {
            return Result<CommentSubmissionAcknowledgement>.NotFound("Blog post was not found.");
        }

        var comment = new BlogComment(
            Guid.NewGuid(),
            post.Id,
            request.AuthorName,
            request.AuthorEmail,
            request.Content);

        dbContext.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<CommentSubmissionAcknowledgement>.Success(
            new CommentSubmissionAcknowledgement("Thank you. Your comment is awaiting moderation."));
    }

    private async Task<IReadOnlyCollection<StorefrontProductListItemDto>> GetRelatedProductsAsync(
        Guid postId,
        CancellationToken cancellationToken)
    {
        var productIds = await dbContext.BlogPostRelatedProducts
            .AsNoTrackingIfEf()
            .Where(rp => rp.BlogPostId == postId)
            .OrderBy(rp => rp.ProductId)
            .Select(rp => rp.ProductId)
            .Take(MaxRelatedProducts)
            .ToArrayAsyncSafe(cancellationToken);

        if (productIds.Length == 0)
        {
            return Array.Empty<StorefrontProductListItemDto>();
        }

        var products = await dbContext.Products
            .AsNoTrackingIfEf()
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .OrderBy(p => p.Slug)
            .ToArrayAsyncSafe(cancellationToken);

        var categories = (await dbContext.Categories
            .AsNoTrackingIfEf()
            .Where(c => c.IsActive)
            .ToArrayAsyncSafe(cancellationToken))
            .ToDictionary(c => c.Id);

        var variants = (await dbContext.ProductVariants
            .AsNoTrackingIfEf()
            .Where(v => productIds.Contains(v.ProductId) && v.IsActive)
            .OrderBy(v => v.Sku)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(v => v.ProductId);

        var images = (await dbContext.ProductImages
            .AsNoTrackingIfEf()
            .Where(i => productIds.Contains(i.ProductId))
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.ImageUrl)
            .ToArrayAsyncSafe(cancellationToken))
            .ToLookup(i => i.ProductId);

        var list = new List<StorefrontProductListItemDto>();
        foreach (var product in products)
        {
            categories.TryGetValue(product.CategoryId, out var category);
            var categoryName = category?.Name.Get(languageProvider.CurrentLanguage) ?? "Unknown";

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
                product.Name.Get(languageProvider.CurrentLanguage),
                product.Slug,
                product.Description?.Get(languageProvider.CurrentLanguage),
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
            comment.ModerationStatus,
            comment.CreatedAt,
            comment.ModeratedAt,
            comment.ModeratedBy);
    }
}
