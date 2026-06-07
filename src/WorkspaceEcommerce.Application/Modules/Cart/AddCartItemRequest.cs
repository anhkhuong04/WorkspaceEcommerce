namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class AddCartItemRequest
{
    public string SessionId { get; init; } = string.Empty;

    public Guid ProductVariantId { get; init; }

    public int Quantity { get; init; }
}
