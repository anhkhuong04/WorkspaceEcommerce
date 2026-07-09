using System;
using System.Linq;
using System.Threading.Tasks;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Blogs;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Application.Tests.Modules.Blogs;

public sealed class StorefrontBlogServiceTests
{
    [Fact]
    public async Task GetPublishedBlogPostsAsync_ReturnsOnlyPublished()
    {
        var published = new BlogPost(Guid.NewGuid(), "Published Post", "pub", "Summary", "Content", null, true);
        var draft = new BlogPost(Guid.NewGuid(), "Draft Post", "draft", "Summary", "Content", null, false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(published, draft);
        var service = CreateService(dbContext);

        var result = await service.GetPublishedBlogPostsAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value);
        Assert.Equal(published.Id, result.Value.First().Id);
    }

    [Fact]
    public async Task GetBlogPostBySlugAsync_PublishedPost_ReturnsPost()
    {
        var post = new BlogPost(Guid.NewGuid(), "Post", "slug", "Summary", "Content", null, true);
        var comment = new BlogComment(Guid.NewGuid(), post.Id, "Author", "author@test.com", "Nice!", true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(post);
        dbContext.Seed(comment);
        var service = CreateService(dbContext);

        var result = await service.GetBlogPostBySlugAsync("slug");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(post.Id, result.Value.Id);
        Assert.Single(result.Value.Comments);
        Assert.Equal("Nice!", result.Value.Comments.First().Content);
    }

    [Fact]
    public async Task GetBlogPostBySlugAsync_UnpublishedPost_ReturnsNotFound()
    {
        var post = new BlogPost(Guid.NewGuid(), "Post", "slug", "Summary", "Content", null, false);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(post);
        var service = CreateService(dbContext);

        var result = await service.GetBlogPostBySlugAsync("slug");

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SubmitCommentAsync_ValidComment_AddsComment()
    {
        var post = new BlogPost(Guid.NewGuid(), "Post", "slug", "Summary", "Content", null, true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(post);
        var service = CreateService(dbContext);

        var result = await service.SubmitCommentAsync("slug", new CreateCommentRequest(
            "John",
            "john@test.com",
            "Great article!"
        ));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(dbContext.BlogComments);
        Assert.Equal("Great article!", dbContext.BlogComments.First().Content);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    private static StorefrontBlogService CreateService(FakeAppDbContext dbContext)
    {
        return new StorefrontBlogService(
            dbContext,
            new CreateCommentRequestValidator());
    }
}
