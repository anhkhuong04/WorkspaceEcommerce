namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

public sealed record AdminBannerDto(
    Guid Id,
    string Title,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
