using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Blogs;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Tests.Modules.Blogs;

public sealed class AdminBlogServiceTests
{
    [Fact]
    public async Task CreateBlogPostAsync_ValidRequest_CreatesBlogPost()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateBlogPostAsync(new CreateBlogPostRequest(
            "Test Article",
            "test-article",
            "This is a test summary",
            "This is test content for the article.",
            "https://example.test/image.jpg",
            true,
            Array.Empty<Guid>()));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Test Article", result.Value.Title);
        Assert.Equal("test-article", result.Value.Slug);
        Assert.True(result.Value.IsPublished);
        Assert.Single(dbContext.BlogPosts);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateBlogPostAsync_DuplicateSlug_ReturnsConflict()
    {
        var existing = new BlogPost(Guid.NewGuid(), "Existing", "test-article", "Summary", "Content", null, true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(existing);
        var service = CreateService(dbContext);

        var result = await service.CreateBlogPostAsync(new CreateBlogPostRequest(
            "New Article",
            "test-article",
            "Summary",
            "Content",
            null,
            false,
            Array.Empty<Guid>()));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Single(dbContext.BlogPosts);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBlogPostAsync_ExistingPost_UpdatesPostDetails()
    {
        var post = new BlogPost(Guid.NewGuid(), "Original Title", "original-slug", "Original Summary", "Original Content", "https://example.test/orig.jpg", false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(post);
        var service = CreateService(dbContext);

        var result = await service.UpdateBlogPostAsync(post.Id, new UpdateBlogPostRequest(
            "Updated Title",
            "updated-slug",
            "Updated Summary",
            "Updated Content",
            "https://example.test/up.jpg",
            true,
            Array.Empty<Guid>()));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Title", result.Value.Title);
        Assert.Equal("updated-slug", result.Value.Slug);
        Assert.True(result.Value.IsPublished);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBlogPostAsync_MissingPost_ReturnsNotFound()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.UpdateBlogPostAsync(Guid.NewGuid(), new UpdateBlogPostRequest(
            "Title",
            "slug",
            "Summary",
            "Content",
            null,
            false,
            Array.Empty<Guid>()));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeleteBlogPostAsync_ExistingPost_DeletesPost()
    {
        var post = new BlogPost(Guid.NewGuid(), "Title", "slug", "Summary", "Content", null, true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(post);
        var service = CreateService(dbContext);

        var result = await service.DeleteBlogPostAsync(post.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.BlogPosts);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task TogglePublishStatusAsync_TogglesPublished()
    {
        var post = new BlogPost(Guid.NewGuid(), "Title", "slug", "Summary", "Content", null, false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(post);
        var service = CreateService(dbContext);

        var result = await service.TogglePublishStatusAsync(post.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPublished);
        Assert.True(post.IsPublished);

        var secondResult = await service.TogglePublishStatusAsync(post.Id);
        Assert.True(secondResult.IsSuccess);
        Assert.False(secondResult.Value.IsPublished);
        Assert.False(post.IsPublished);
    }

    private static AdminBlogService CreateService(FakeAppDbContext dbContext)
    {
        return new AdminBlogService(
            dbContext,
            new CreateBlogPostRequestValidator(),
            new UpdateBlogPostRequestValidator());
    }
}
