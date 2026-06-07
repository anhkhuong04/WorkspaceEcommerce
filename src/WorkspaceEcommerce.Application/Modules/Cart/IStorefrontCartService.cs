using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Cart;

public interface IStorefrontCartService
{
    Task<Result<CartDto>> GetCartAsync(GetCartRequest request, CancellationToken cancellationToken = default);

    Task<Result<CartDto>> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default);

    Task<Result<CartDto>> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CartDto>> RemoveItemAsync(
        Guid itemId,
        RemoveCartItemRequest request,
        CancellationToken cancellationToken = default);
}
