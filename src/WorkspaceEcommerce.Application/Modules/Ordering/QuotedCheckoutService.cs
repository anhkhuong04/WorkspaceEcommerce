using System.Text.Json;
using System.Diagnostics;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Payments;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public interface IQuotedCheckoutService
{
    Task<Result<CheckoutResponse>> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken);
}

internal sealed class QuotedCheckoutService(ICheckoutStore checkout, IShippingCheckoutStore shipping,
    IAppDbContext db, ICurrentCustomerContext customer, ICurrentLanguageProvider language,
    IShippingAdministrativeCatalog catalog, IVNPayPaymentService vnPay, TimeProvider clock,
    IValidator<CheckoutRequest> validator, IFulfillmentCoordinator fulfillment, IOrderShippingReader shippingReader, Microsoft.Extensions.Options.IOptions<OrderExpiryOptions>? expiryOptions = null) : IQuotedCheckoutService
{
    private readonly CheckoutCartBuilder builder = new(checkout, language);
    private readonly CheckoutCouponApplier coupons = new(checkout, customer);
    private readonly CheckoutOrderFactory factory = new(checkout, expiryOptions?.Value);

    public async Task<Result<CheckoutResponse>> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var outcome = "Error";
        try
        {
            var result = await CheckoutCoreAsync(request, cancellationToken);
            outcome = result.Status.ToString();
            return result;
        }
        finally { ShipmentIntegrationMetrics.RecordShippingRequest("accept", request.ShippingMethod, outcome, Stopwatch.GetElapsedTime(started).TotalSeconds); }
    }
    private async Task<Result<CheckoutResponse>> CheckoutCoreAsync(CheckoutRequest request, CancellationToken cancellationToken)
    {
        if (!request.ShippingQuoteId.HasValue || request.ShippingQuoteId == Guid.Empty)
            return Result<CheckoutResponse>.Validation(["A shipping quote is required."]);
        if (!request.ShippingMethod.HasValue || !Enum.IsDefined(request.ShippingMethod.Value)
            || string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.CustomerPhone)
            || string.IsNullOrWhiteSpace(request.SessionId) || request.SessionId.Length > 128
            || string.IsNullOrWhiteSpace(request.ShippingStreet) || request.ShippingStreet.Length > 250)
            return Result<CheckoutResponse>.Validation(["Shipping method, session and street are required."]);
        try
        {
            return await shipping.TransactionAsync(async ct =>
            {
                // Quote first serializes retries even after the cart has been removed.
                var quote = await shipping.LockQuoteAsync(request.ShippingQuoteId.Value, ct);
                if (quote is null || quote.SubjectHash != ShippingQuoteFingerprint.Subject(customer.CustomerId, request.SessionId))
                    return Result<CheckoutResponse>.NotFound("Shipping quote was not found.");
                var acceptance = ShippingQuoteFingerprint.Acceptance(request);
                if (quote.OrderId.HasValue)
                {
                    if (quote.CheckoutRequestHash != acceptance) return Result<CheckoutResponse>.Conflict("This quote was accepted with different checkout details.");
                    var accepted = await db.Orders.Include(x => x.Items).SingleAsync(x => x.Id == quote.OrderId, ct);
                    var payment = await db.PaymentTransactions.SingleOrDefaultAsync(x => x.OrderId == accepted.Id, ct);
                    return await ResponseAsync(accepted, payment, request.ClientIpAddress, ct);
                }
                var now = clock.GetUtcNow();
                if (quote.Method != request.ShippingMethod || quote.RevokedAtUtc.HasValue || now < quote.CreatedAtUtc || now >= quote.ExpiresAtUtc)
                {
                    ShipmentIntegrationMetrics.RecordCheckoutRejection(quote.Method, quote.RevokedAtUtc.HasValue ? "revoked" : now >= quote.ExpiresAtUtc ? "expired" : "method_or_time");
                    return Result<CheckoutResponse>.Conflict("Shipping quote is unavailable or expired. Request and confirm a new quote.");
                }
                var snapshot = JsonSerializer.Deserialize<ShippingInputSnapshot>(quote.SnapshotJson);
                if (snapshot is null || snapshot.Version != 1 || snapshot.Destination.ProvinceCode != request.ShippingProvinceCode
                    || snapshot.Destination.WardCode != request.ShippingWardCode)
                    return Result<CheckoutResponse>.Conflict("Shipping inputs changed. Request and confirm a new quote.");
                var destination = catalog.Resolve(request.ShippingProvinceCode!, request.ShippingWardCode!);
                if (destination is null || destination.CatalogVersion != snapshot.Destination.CatalogVersion)
                    return Result<CheckoutResponse>.Conflict("Administrative catalog changed. Request a new quote.");
                var canonical = Canonical(request, snapshot.Destination);
                var validation = await validator.ValidateAsync(canonical, ct);
                if (!validation.IsValid) return Result<CheckoutResponse>.Validation(validation.Errors.Select(x => x.ErrorMessage));
                var cart = await shipping.LockCartAsync(request.SessionId.Trim(), ct);
                if (cart is null || cart.Items.Count == 0 || cart.CustomerId.HasValue && cart.CustomerId != customer.CustomerId)
                    return Result<CheckoutResponse>.Conflict("Cart is unavailable. Request a new quote.");
                var items = await builder.BuildItemSnapshotsAsync(cart, true, ct);
                if (items.IsFailure) return CheckoutResultMapper.ToCheckoutFailure(items);
                CheckoutCouponEvaluation? coupon = null;
                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {
                    var evaluation = await coupons.EvaluateAsync(request.CouponCode, items.Value!, true, ct);
                    if (evaluation.IsFailure) return CheckoutResultMapper.ToCheckoutFailure(evaluation);
                    coupon = evaluation.Value!;
                    if (CheckoutCouponApplier.IsUsageLimitReached(coupon.Coupon)) return Result<CheckoutResponse>.Conflict("Coupon usage limit has been reached.");
                }
                var input = new ShippingQuoteInput
                {
                    SessionId = request.SessionId,
                    ShippingStreet = request.ShippingStreet,
                    ProvinceCode = request.ShippingProvinceCode!,
                    WardCode = request.ShippingWardCode!,
                    Method = request.ShippingMethod.Value,
                    PaymentMethod = request.PaymentMethod,
                    CouponCode = request.CouponCode
                };
                if (quote.InputFingerprint != ShippingQuoteFingerprint.Inputs(input, destination.CatalogVersion, coupon?.DiscountAmount ?? 0, items.Value!))
                {
                    ShipmentIntegrationMetrics.RecordCheckoutRejection(quote.Method, "input_mismatch");
                    return Result<CheckoutResponse>.Conflict("Cart, address or payment inputs changed. Request and confirm a new quote.");
                }
                var order = await factory.CreateAsync(canonical, customer.CustomerId, items.Value!, ct);
                order.SetShippingFee(quote.CustomerCharge);
                if (coupon is not null)
                {
                    order.ApplyCoupon(coupon.Coupon.Id, coupon.Coupon.Code, coupon.Coupon.Name, coupon.DiscountAmount);
                    coupon.Coupon.ReserveUsage();
                    checkout.Update(coupon.Coupon);
                    checkout.Add(new CouponRedemption(Guid.NewGuid(), coupon.Coupon.Id, order.Id, customer.CustomerId, order.CustomerPhone, coupon.Coupon.Code, coupon.DiscountAmount));
                }
                PaymentTransaction? transaction = null;
                if (order.PaymentMethod == PaymentMethod.VNPay)
                {
                    transaction = new PaymentTransaction(Guid.NewGuid(), order.Id, PaymentProvider.VNPay, order.TotalAmount, order.CurrencyCode,
                        $"{order.OrderCode}-{Guid.NewGuid():N}"[..32].ToUpperInvariant());
                    checkout.Add(transaction);
                }
                foreach (var item in items.Value!) { item.Variant.DecreaseStock(item.Quantity); checkout.Update(item.Variant); }
                checkout.Add(order);
                quote.Consume(order.Id, now, acceptance);
                var selection = new OrderShippingSelection(Guid.NewGuid(), quote);
                checkout.Add(selection);
                checkout.Remove(cart);
                await checkout.SaveChangesAsync(ct);
                await fulfillment.AcceptAsync(order, selection.Method, ct);
                await checkout.SaveChangesAsync(ct);
                return await ResponseAsync(order, transaction, request.ClientIpAddress, ct);
            }, cancellationToken);
        }
        catch (DomainException exception) { return Result<CheckoutResponse>.Conflict(exception.Message); }
        catch (PersistenceConcurrencyException) { return Result<CheckoutResponse>.Conflict("Checkout changed concurrently. Retry the same quote."); }
    }
    private async Task<Result<CheckoutResponse>> ResponseAsync(Order order, PaymentTransaction? payment, string? ip, CancellationToken ct)
    {
        var url = order.PaymentMethod == PaymentMethod.VNPay && order.PaymentStatus != PaymentStatus.Paid && payment?.Status == PaymentTransactionStatus.Pending
            ? vnPay.CreatePaymentUrl(new VNPayCreatePaymentUrlRequest { TxnRef = payment.TxnRef, Amount = payment.Amount, OrderInfo = $"Pay order {order.OrderCode}", IpAddress = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim() }) : null;
        var dto = CheckoutOrderMapper.ToDto(order) with { Shipping = await shippingReader.GetAsync(order.Id, ct) };
        return Result<CheckoutResponse>.Success(new(dto, url is not null, url));
    }
    private static CheckoutRequest Canonical(CheckoutRequest request, ShippingDestination destination) => new()
    {
        SessionId = request.SessionId.Trim(),
        CustomerName = request.CustomerName,
        CustomerPhone = request.CustomerPhone,
        CustomerEmail = request.CustomerEmail,
        ShippingStreet = request.ShippingStreet.Trim(),
        ShippingWard = destination.WardName,
        ShippingProvince = destination.ProvinceName,
        ShippingAddress = $"{request.ShippingStreet.Trim()}, {destination.WardName}, {destination.ProvinceName}",
        Note = request.Note,
        PaymentMethod = request.PaymentMethod,
        CouponCode = request.CouponCode,
        ShippingQuoteId = request.ShippingQuoteId,
        ShippingMethod = request.ShippingMethod,
        ShippingProvinceCode = request.ShippingProvinceCode,
        ShippingWardCode = request.ShippingWardCode,
        ClientIpAddress = request.ClientIpAddress
    };
}
