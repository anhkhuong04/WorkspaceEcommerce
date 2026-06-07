namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed record CartDto(
    Guid Id,
    Guid? CustomerId,
    string? SessionId,
    IReadOnlyCollection<CartItemDto> Items,
    int TotalQuantity,
    decimal TotalAmount);
