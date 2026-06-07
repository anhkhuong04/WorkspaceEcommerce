namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed record CartItemDto(
    Guid Id,
    Guid ProductVariantId,
    int Quantity,
    decimal UnitPriceSnapshot,
    decimal LineTotal);
