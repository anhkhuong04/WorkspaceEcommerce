using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutService(
    ICheckoutStore checkoutStore,
    IValidator<CheckoutRequest> validator) : ICheckoutService
{
    public async Task<Result<CheckoutResponse>> CheckoutAsync(
        CheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<CheckoutResponse>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var sessionId = NormalizeSessionId(request.SessionId);
        var cart = await checkoutStore.FindCartBySessionIdAsync(sessionId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return Result<CheckoutResponse>.Validation(["Cart is empty."]);
        }

        Order? order = null;
        Result<CheckoutResponse>? failure = null;
        try
        {
            await checkoutStore.ExecuteInTransactionAsync(async transactionCancellationToken =>
            {
                var itemSnapshotsResult = await BuildItemSnapshotsAsync(cart, transactionCancellationToken);
                if (itemSnapshotsResult.IsFailure)
                {
                    failure = ToCheckoutFailure(itemSnapshotsResult);
                    return;
                }

                var snapshots = itemSnapshotsResult.Value!;
                order = await CreateOrderAsync(request, snapshots, transactionCancellationToken);

                foreach (var snapshot in snapshots)
                {
                    snapshot.Variant.DecreaseStock(snapshot.Quantity);
                    checkoutStore.Update(snapshot.Variant);
                }

                checkoutStore.Add(order);
                checkoutStore.Remove(cart);
                await checkoutStore.SaveChangesAsync(transactionCancellationToken);
            }, cancellationToken);
        }
        catch (DomainException exception)
        {
            return Result<CheckoutResponse>.Validation([exception.Message]);
        }

        if (failure is not null)
        {
            return failure;
        }

        return Result<CheckoutResponse>.Success(new CheckoutResponse(ToDto(order!)));
    }

    private async Task<Result<IReadOnlyCollection<CheckoutItemSnapshot>>> BuildItemSnapshotsAsync(
        CartAggregate cart,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<CheckoutItemSnapshot>();

        foreach (var cartItem in cart.Items)
        {
            var variant = await checkoutStore.FindProductVariantByIdAsync(cartItem.ProductVariantId, cancellationToken);
            if (variant is null || !variant.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            var product = await checkoutStore.FindProductByIdAsync(variant.ProductId, cancellationToken);
            if (product is null || !product.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            var category = await checkoutStore.FindCategoryByIdAsync(product.CategoryId, cancellationToken);
            if (category is null || !category.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            if (cartItem.Quantity > variant.StockQuantity)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.Conflict("Requested quantity exceeds available stock.");
            }

            snapshots.Add(new CheckoutItemSnapshot(
                variant,
                product.Name,
                variant.Sku,
                cartItem.UnitPriceSnapshot,
                cartItem.Quantity,
                variant.RequiresInstallation));
        }

        return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.Success(snapshots);
    }

    private async Task<Order> CreateOrderAsync(
        CheckoutRequest request,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var order = new Order(
            Guid.NewGuid(),
            await GenerateOrderCodeAsync(cancellationToken),
            null,
            request.CustomerName,
            request.CustomerPhone,
            request.CustomerEmail,
            request.ShippingAddress,
            request.Note,
            request.PaymentMethod);

        foreach (var snapshot in snapshots)
        {
            order.AddItem(
                Guid.NewGuid(),
                snapshot.Variant.Id,
                snapshot.ProductNameSnapshot,
                snapshot.SkuSnapshot,
                snapshot.UnitPrice,
                snapshot.Quantity,
                snapshot.RequiresInstallation);
        }

        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }

    private async Task<string> GenerateOrderCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var orderCode = $"ORD-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..21].ToUpperInvariant();
            if (!await checkoutStore.OrderCodeExistsAsync(orderCode, cancellationToken))
            {
                return orderCode;
            }
        }

        throw new DomainException("Could not generate a unique order code.");
    }

    private static OrderDto ToDto(Order order)
    {
        return new OrderDto(
            order.Id,
            order.OrderCode,
            order.CustomerId,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingAddress,
            order.Note,
            order.Subtotal,
            order.ShippingFee,
            order.DiscountAmount,
            order.TotalAmount,
            order.Status,
            order.PaymentMethod,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(ToDto).ToArray());
    }

    private static OrderItemDto ToDto(OrderItem item)
    {
        return new OrderItemDto(
            item.Id,
            item.ProductVariantId,
            item.ProductNameSnapshot,
            item.SkuSnapshot,
            item.UnitPrice,
            item.Quantity,
            item.LineTotal,
            item.RequiresInstallation);
    }

    private static Result<CheckoutResponse> ToCheckoutFailure<T>(Result<T> result)
    {
        return result.Status switch
        {
            ResultStatus.Validation => Result<CheckoutResponse>.Validation(result.Errors),
            ResultStatus.NotFound => Result<CheckoutResponse>.NotFound(result.FirstError ?? "Resource was not found."),
            ResultStatus.Conflict => Result<CheckoutResponse>.Conflict(result.FirstError ?? "A conflict occurred."),
            ResultStatus.Unauthorized => Result<CheckoutResponse>.Unauthorized(result.FirstError ?? "Unauthorized."),
            _ => Result<CheckoutResponse>.Failure(result.Errors)
        };
    }

    private static string NormalizeSessionId(string sessionId)
    {
        return sessionId.Trim();
    }

    private sealed record CheckoutItemSnapshot(
        ProductVariant Variant,
        string ProductNameSnapshot,
        string SkuSnapshot,
        decimal UnitPrice,
        int Quantity,
        bool RequiresInstallation);
}
