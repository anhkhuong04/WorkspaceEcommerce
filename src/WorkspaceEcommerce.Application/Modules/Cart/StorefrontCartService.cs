using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Cart;

internal sealed class StorefrontCartService(
    ICartStore cartStore,
    IValidator<GetCartRequest> getCartValidator,
    IValidator<AddCartItemRequest> addItemValidator,
    IValidator<UpdateCartItemRequest> updateItemValidator,
    IValidator<RemoveCartItemRequest> removeItemValidator) : IStorefrontCartService
{
    public async Task<Result<CartDto>> GetCartAsync(
        GetCartRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await getCartValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CartDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var sessionId = NormalizeSessionId(request.SessionId);
        var cart = await cartStore.FindCartBySessionIdAsync(sessionId, cancellationToken);

        return Result<CartDto>.Success(cart is null ? EmptyCart(sessionId) : await ToDtoAsync(cart, cancellationToken));
    }

    public async Task<Result<CartDto>> AddItemAsync(
        AddCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await addItemValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CartDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var variantResult = await GetAvailableVariantAsync(request.ProductVariantId, cancellationToken);
        if (variantResult.IsFailure)
        {
            return ToCartFailure(variantResult);
        }

        var sessionId = NormalizeSessionId(request.SessionId);
        var cart = await cartStore.FindCartBySessionIdAsync(sessionId, cancellationToken);
        var isNewCart = cart is null;
        cart ??= new Domain.Modules.Cart.Cart(Guid.NewGuid(), null, sessionId);

        var existingQuantity = cart.Items
            .FirstOrDefault(item => item.ProductVariantId == request.ProductVariantId)
            ?.Quantity ?? 0;

        if (existingQuantity + request.Quantity > variantResult.Value!.StockQuantity)
        {
            return Result<CartDto>.Conflict("Requested quantity exceeds available stock.");
        }

        try
        {
            var hadExistingItem = existingQuantity > 0;
            var item = cart.AddItem(Guid.NewGuid(), request.ProductVariantId, request.Quantity, variantResult.Value.Price);

            if (isNewCart)
            {
                cartStore.Add(cart);
            }
            else
            {
                if (!hadExistingItem)
                {
                    cartStore.Add(item);
                }

                cartStore.Update(cart);
            }

            await cartStore.SaveChangesAsync(cancellationToken);

            return Result<CartDto>.Success(await ToDtoAsync(cart, cancellationToken));
        }
        catch (DomainException exception)
        {
            return Result<CartDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<CartDto>> UpdateItemAsync(
        Guid itemId,
        UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
        {
            return Result<CartDto>.Validation(["Cart item id is required."]);
        }

        var validationResult = await updateItemValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CartDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var cartResult = await GetCartForMutationAsync(request.SessionId, itemId, cancellationToken);
        if (cartResult.IsFailure)
        {
            return ToCartFailure(cartResult);
        }

        var cart = cartResult.Value!;
        var item = cart.Items.First(existing => existing.Id == itemId);
        var variantResult = await GetAvailableVariantAsync(item.ProductVariantId, cancellationToken);
        if (variantResult.IsFailure)
        {
            return ToCartFailure(variantResult);
        }

        if (request.Quantity > variantResult.Value!.StockQuantity)
        {
            return Result<CartDto>.Conflict("Requested quantity exceeds available stock.");
        }

        try
        {
            cart.UpdateItemQuantity(itemId, request.Quantity);
            cartStore.Update(cart);
            await cartStore.SaveChangesAsync(cancellationToken);

            return Result<CartDto>.Success(await ToDtoAsync(cart, cancellationToken));
        }
        catch (DomainException exception)
        {
            return Result<CartDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<CartDto>> RemoveItemAsync(
        Guid itemId,
        RemoveCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
        {
            return Result<CartDto>.Validation(["Cart item id is required."]);
        }

        var validationResult = await removeItemValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CartDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var cartResult = await GetCartForMutationAsync(request.SessionId, itemId, cancellationToken);
        if (cartResult.IsFailure)
        {
            return ToCartFailure(cartResult);
        }

        try
        {
            var cart = cartResult.Value!;
            var removedItem = cart.RemoveItem(itemId);

            cartStore.Remove(removedItem);
            cartStore.Update(cart);
            await cartStore.SaveChangesAsync(cancellationToken);

            return Result<CartDto>.Success(await ToDtoAsync(cart, cancellationToken));
        }
        catch (DomainException exception)
        {
            return Result<CartDto>.Validation([exception.Message]);
        }
    }

    private async Task<Result<ProductVariant>> GetAvailableVariantAsync(
        Guid productVariantId,
        CancellationToken cancellationToken)
    {
        var variant = await cartStore.FindProductVariantByIdAsync(productVariantId, cancellationToken);
        if (variant is null || !variant.IsActive)
        {
            return Result<ProductVariant>.NotFound("Product variant was not found.");
        }

        var product = await cartStore.FindProductByIdAsync(variant.ProductId, cancellationToken);
        if (product is null || !product.IsActive)
        {
            return Result<ProductVariant>.NotFound("Product variant was not found.");
        }

        var category = await cartStore.FindCategoryByIdAsync(product.CategoryId, cancellationToken);
        if (category is null || !category.IsActive)
        {
            return Result<ProductVariant>.NotFound("Product variant was not found.");
        }

        if (variant.StockQuantity <= 0)
        {
            return Result<ProductVariant>.Conflict("Product variant is out of stock.");
        }

        return Result<ProductVariant>.Success(variant);
    }

    private async Task<Result<Domain.Modules.Cart.Cart>> GetCartForMutationAsync(
        string sessionId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var cart = await cartStore.FindCartBySessionIdAsync(normalizedSessionId, cancellationToken);
        if (cart is null || cart.Items.All(item => item.Id != itemId))
        {
            return Result<Domain.Modules.Cart.Cart>.NotFound("Cart item was not found.");
        }

        return Result<Domain.Modules.Cart.Cart>.Success(cart);
    }

    private async Task<CartDto> ToDtoAsync(
        Domain.Modules.Cart.Cart cart,
        CancellationToken cancellationToken)
    {
        return new CartDto(
            cart.Id,
            cart.CustomerId,
            cart.SessionId,
            await ToItemDtosAsync(cart.Items, cancellationToken),
            cart.TotalQuantity,
            cart.TotalAmount);
    }

    private async Task<IReadOnlyCollection<CartItemDto>> ToItemDtosAsync(
        IEnumerable<CartItem> items,
        CancellationToken cancellationToken)
    {
        var result = new List<CartItemDto>();
        foreach (var item in items)
        {
            result.Add(await ToItemDtoAsync(item, cancellationToken));
        }

        return result;
    }

    private async Task<CartItemDto> ToItemDtoAsync(
        CartItem item,
        CancellationToken cancellationToken)
    {
        var variant = await cartStore.FindProductVariantByIdAsync(item.ProductVariantId, cancellationToken);
        var product = variant is null
            ? null
            : await cartStore.FindProductByIdAsync(variant.ProductId, cancellationToken);
        var image = product is null
            ? null
            : await cartStore.FindPrimaryProductImageByProductIdAsync(product.Id, cancellationToken);

        return new CartItemDto(
            item.Id,
            item.ProductVariantId,
            variant?.ProductId ?? Guid.Empty,
            product?.Name ?? "Product",
            product?.Slug ?? string.Empty,
            variant?.Name ?? "Variant",
            variant?.Color,
            variant?.Size,
            image?.ImageUrl,
            item.Quantity,
            item.UnitPriceSnapshot,
            item.LineTotal);
    }

    private static CartDto EmptyCart(string sessionId)
    {
        return new CartDto(Guid.Empty, null, sessionId, [], 0, 0m);
    }

    private static string NormalizeSessionId(string sessionId)
    {
        return sessionId.Trim();
    }

    private static Result<CartDto> ToCartFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CartDto>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CartDto>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CartDto>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CartDto>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CartDto>.Failure(result.Errors)
        };
    }
}
