namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed record CartItemDto(
    Guid Id,
    Guid ProductVariantId,
    Guid ProductId,
    string ProductName,
    string ProductSlug,
    string VariantName,
    string? VariantColor,
    string? VariantSize,
    string? ImageUrl,
    int Quantity,
    decimal UnitPriceSnapshot,
    decimal LineTotal);
