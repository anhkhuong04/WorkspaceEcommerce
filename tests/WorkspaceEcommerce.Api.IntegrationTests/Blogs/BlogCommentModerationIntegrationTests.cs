using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Blogs;

namespace WorkspaceEcommerce.Api.IntegrationTests.Blogs;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class BlogCommentModerationIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task PublicComment_IsPendingUntilAnAdminApprovesIt()
    {
        await fixture.ResetDatabaseAsync();
        var post = await SeedPublishedPostAsync();
        using var client = fixture.CreateClient();

        using var submission = await client.PostAsJsonAsync(
            $"/api/blog-posts/{post.Slug}/comments",
            new { authorName = "Commenter", authorEmail = "commenter@example.test", content = "<script>alert(1)</script>" });
        var submissionJson = await submission.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Accepted, submission.StatusCode);
        Assert.Contains("awaiting moderation", submissionJson["data"]!["message"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(submissionJson["data"]!["content"]);

        using var beforeApproval = await client.GetAsync($"/api/blog-posts/{post.Slug}");
        var beforeApprovalJson = await beforeApproval.ReadJsonAsync();
        Assert.Empty(beforeApprovalJson["data"]!["comments"]!.AsArray());

        var pendingCommentId = await fixture.ExecuteDbAsync(async dbContext =>
            await Task.FromResult(dbContext.BlogComments.Single().Id));
        client.UseBearerToken(await client.LoginAsAdminAsync());
        using var approval = await client.PostAsync($"/api/admin/blog-comments/{pendingCommentId}/approve", null);

        Assert.Equal(HttpStatusCode.OK, approval.StatusCode);
        using var afterApproval = await client.GetAsync($"/api/blog-posts/{post.Slug}");
        var afterApprovalJson = await afterApproval.ReadJsonAsync();
        var comment = Assert.Single(afterApprovalJson["data"]!["comments"]!.AsArray());
        Assert.Equal("<script>alert(1)</script>", comment!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task ModerationEndpoints_RequireAdminAuthentication()
    {
        await fixture.ResetDatabaseAsync();
        var post = await SeedPublishedPostAsync();
        var comment = new BlogComment(Guid.NewGuid(), post.Id, "Commenter", "commenter@example.test", "Pending");
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.BlogComments.Add(comment);
            return Task.CompletedTask;
        });
        using var client = fixture.CreateClient();

        using var response = await client.PostAsync($"/api/admin/blog-comments/{comment.Id}/approve", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<BlogPost> SeedPublishedPostAsync()
    {
        var post = new BlogPost(Guid.NewGuid(), "Post", $"post-{Guid.NewGuid():N}", "Summary", "Content", null, true);
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.BlogPosts.Add(post);
            return Task.CompletedTask;
        });
        return post;
    }
}
