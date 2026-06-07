namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class RemoveCartItemRequest
{
    public string SessionId { get; init; } = string.Empty;
}
