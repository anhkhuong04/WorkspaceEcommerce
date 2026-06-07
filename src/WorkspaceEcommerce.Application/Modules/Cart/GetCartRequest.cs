namespace WorkspaceEcommerce.Application.Modules.Cart;

public sealed class GetCartRequest
{
    public string SessionId { get; init; } = string.Empty;
}
