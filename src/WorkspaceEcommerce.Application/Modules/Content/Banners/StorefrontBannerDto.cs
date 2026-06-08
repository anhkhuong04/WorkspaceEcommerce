namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

public sealed record StorefrontBannerDto(
    Guid Id,
    string Title,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder);
