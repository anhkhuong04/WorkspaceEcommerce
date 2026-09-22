using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Documents;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering.Receipts;

internal sealed class OrderReceiptService(
    IAppDbContext dbContext,
    ICurrentCustomerContext currentCustomer,
    ICurrentLanguageProvider languageProvider,
    IValidator<OrderLookupRequest> lookupValidator,
    IOrderReceiptRenderer renderer,
    TimeProvider timeProvider,
    IOrderReceiptImageResolver? imageResolver = null) : IOrderReceiptService
{
    private const string PdfContentType = "application/pdf";

    public async Task<Result<OrderReceiptDocument>> GenerateForGuestAsync(
        OrderLookupRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await lookupValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<OrderReceiptDocument>.Validation(
                validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var orderCode = request.OrderCode.Trim().ToUpperInvariant();
        var phone = request.Phone.Trim();
        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(existing => existing.OrderCode == orderCode && existing.CustomerPhone == phone)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return order is null
            ? Result<OrderReceiptDocument>.NotFound("Order was not found.")
            : await RenderAsync(order, cancellationToken);
    }

    public async Task<Result<OrderReceiptDocument>> GenerateForCustomerAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var customerId = currentCustomer.CustomerId;
        if (!customerId.HasValue)
        {
            return Result<OrderReceiptDocument>.Unauthorized("Customer authentication is required.");
        }

        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(existing => existing.Id == orderId && existing.CustomerId == customerId.Value)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return order is null
            ? Result<OrderReceiptDocument>.NotFound("Order was not found.")
            : await RenderAsync(order, cancellationToken);
    }

    public async Task<Result<OrderReceiptDocument>> GenerateForAdminAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .AsNoTrackingIfEf()
            .Where(existing => existing.Id == orderId)
            .FirstOrDefaultAsyncSafe(cancellationToken);

        return order is null
            ? Result<OrderReceiptDocument>.NotFound("Order was not found.")
            : await RenderAsync(order, cancellationToken);
    }

    private async Task<Result<OrderReceiptDocument>> RenderAsync(
        Order order,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.OrderItems
            .AsNoTrackingIfEf()
            .Where(item => item.OrderId == order.Id)
            .OrderBy(item => item.SkuSnapshot)
            .ThenBy(item => item.Id)
            .ToArrayAsyncSafe(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var imageUrls = items
            .Select(item => item.ProductImageUrlSnapshot)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var imagesByUrl = imageResolver is null || imageUrls.Length == 0
            ? new Dictionary<string, byte[]>(StringComparer.Ordinal)
            : await imageResolver.ResolveAsync(imageUrls, cancellationToken);

        var snapshot = new OrderReceiptSnapshot(
            NormalizeLanguage(languageProvider.CurrentLanguage),
            order.OrderCode,
            order.CustomerName,
            order.CustomerPhone,
            order.CustomerEmail,
            order.ShippingAddress,
            order.Note,
            order.CouponCodeSnapshot,
            order.CouponNameSnapshot,
            order.Subtotal,
            order.ShippingFee,
            order.DiscountAmount,
            order.TotalAmount,
            order.CurrencyCode,
            order.Status,
            order.PaymentMethod,
            order.PaymentStatus,
            order.PaidAt,
            order.CreatedAt,
            timeProvider.GetUtcNow(),
            items.Select(item => new OrderReceiptLineSnapshot(
                item.ProductNameSnapshot,
                item.ProductImageUrlSnapshot is not null && imagesByUrl.TryGetValue(item.ProductImageUrlSnapshot, out var image)
                    ? image
                    : null,
                item.SkuSnapshot,
                item.UnitPrice,
                item.Quantity,
                item.LineTotal,
                item.RequiresInstallation)).ToArray());

        var content = renderer.Render(snapshot);
        return Result<OrderReceiptDocument>.Success(new OrderReceiptDocument(
            content,
            PdfContentType,
            BuildFileName(order.OrderCode)));
    }

    private static string NormalizeLanguage(string? language) =>
        language?.StartsWith("vi", StringComparison.OrdinalIgnoreCase) == true ? "vi" : "en";

    private static string BuildFileName(string orderCode)
    {
        var safeOrderCode = new string(orderCode
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());

        return $"order-receipt-{(string.IsNullOrEmpty(safeOrderCode) ? "order" : safeOrderCode)}.pdf";
    }
}
