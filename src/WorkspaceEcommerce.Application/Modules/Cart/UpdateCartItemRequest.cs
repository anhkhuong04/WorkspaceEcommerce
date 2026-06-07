namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class UpdateCartItemRequest
{
    public string SessionId { get; init; } = string.Empty;

    public int Quantity { get; init; }
}
