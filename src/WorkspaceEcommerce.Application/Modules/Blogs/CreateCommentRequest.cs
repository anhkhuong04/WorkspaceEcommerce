namespace WorkspaceEcommerce.Application.Modules.Blogs;

public sealed record CreateCommentRequest(
    string AuthorName,
    string AuthorEmail,
    string Content);
