namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

public sealed class CreateBannerRequest
{
    public string Title { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;

    public string? LinkUrl { get; init; }

    public int SortOrder { get; init; }

    public bool IsActive { get; init; } = true;
}
