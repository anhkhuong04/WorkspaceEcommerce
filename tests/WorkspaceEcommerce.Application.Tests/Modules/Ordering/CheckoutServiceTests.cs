using Microsoft.Extensions.Logging.Abstractions;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Coupons;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class CheckoutServiceTests
{
    [Fact]
    public async Task CheckoutAsync_ValidCart_CreatesOrderSnapshotsDecreasesStockAndClearsCart()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store, price: 150m, stockQuantity: 5, requiresInstallation: true);
        var cart = new CartAggregate(Guid.NewGuid(), null, "session-1");
        cart.AddItem(Guid.NewGuid(), variant.Id, 2, 120m);
        store.Seed(cart);
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var order = result.Value.Order;
        Assert.StartsWith("ORD-", order.OrderCode, StringComparison.Ordinal);
        Assert.Null(order.CustomerId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(PaymentMethod.Cod, order.PaymentMethod);
        Assert.Equal(240m, order.Subtotal);
        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(0m, order.DiscountAmount);
        Assert.Equal(240m, order.TotalAmount);
        var item = Assert.Single(order.Items);
        Assert.Equal(variant.Id, item.ProductVariantId);
        Assert.Equal("Standing Desk", item.ProductNameSnapshot);
        Assert.Equal("DESK-001", item.SkuSnapshot);
        Assert.Equal(120m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.True(item.RequiresInstallation);
        Assert.Equal(3, variant.StockQuantity);
        Assert.Empty(store.Carts);
        Assert.Single(store.Orders);
        Assert.Equal(1, store.TransactionCallCount);
        Assert.Equal(2, store.SaveChangesCallCount);
    }

    [Fact]
    public async Task CheckoutAsync_WhenCustomerIsAuthenticated_CreatesOrderWithCustomerId()
    {
        var customerId = Guid.NewGuid();
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store);
        SeedCart(store, variant.Id);
        var service = CreateService(store, customerId);

        var result = await service.CheckoutAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(customerId, result.Value.Order.CustomerId);
        Assert.Equal(customerId, store.Orders.Single().CustomerId);
    }

    [Fact]
    public async Task CheckoutAsync_MissingCart_ReturnsValidation()
    {
        var store = new FakeCheckoutStore();
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest());

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Cart is empty.", result.Errors);
        Assert.Empty(store.Orders);
    }

    [Fact]
    public async Task CheckoutAsync_EmptyCart_ReturnsValidation()
    {
        var store = new FakeCheckoutStore();
        store.Seed(new CartAggregate(Guid.NewGuid(), null, "session-1"));
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest());

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Cart is empty.", result.Errors);
        Assert.Empty(store.Orders);
    }

    [Fact]
    public async Task CheckoutAsync_InactiveVariant_ReturnsNotFound()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store, isVariantActive: false);
        SeedCart(store, variant.Id);
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest());

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Product variant was not found.", result.Errors);
        Assert.Empty(store.Orders);
    }

    [Fact]
    public async Task CheckoutAsync_InactiveProductOrCategory_ReturnsNotFound()
    {
        var inactiveProductStore = new FakeCheckoutStore();
        var inactiveProductVariant = SeedVisibleVariant(inactiveProductStore, isProductActive: false);
        SeedCart(inactiveProductStore, inactiveProductVariant.Id);
        var inactiveCategoryStore = new FakeCheckoutStore();
        var inactiveCategoryVariant = SeedVisibleVariant(inactiveCategoryStore, isCategoryActive: false);
        SeedCart(inactiveCategoryStore, inactiveCategoryVariant.Id);

        var inactiveProductResult = await CreateService(inactiveProductStore).CheckoutAsync(CreateRequest());
        var inactiveCategoryResult = await CreateService(inactiveCategoryStore).CheckoutAsync(CreateRequest());

        Assert.Equal(ResultStatus.NotFound, inactiveProductResult.Status);
        Assert.Equal(ResultStatus.NotFound, inactiveCategoryResult.Status);
        Assert.Empty(inactiveProductStore.Orders);
        Assert.Empty(inactiveCategoryStore.Orders);
    }

    [Fact]
    public async Task CheckoutAsync_QuantityExceedsStock_ReturnsConflict()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store, stockQuantity: 1);
        SeedCart(store, variant.Id, quantity: 2);
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest());

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Requested quantity exceeds available stock.", result.Errors);
        Assert.Empty(store.Orders);
        Assert.Equal(1, variant.StockQuantity);
    }

    [Fact]
    public async Task CheckoutAsync_ManualBankTransfer_CreatesOrder()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store);
        SeedCart(store, variant.Id);
        var service = CreateService(store);
        var request = CreateRequest(PaymentMethod.ManualBankTransfer);

        var result = await service.CheckoutAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(PaymentMethod.ManualBankTransfer, result.Value.Order.PaymentMethod);
    }

    [Fact]
    public async Task ValidateCouponAsync_ValidCoupon_ReturnsDiscountPreview()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store);
        SeedCart(store, variant.Id, quantity: 2, unitPriceSnapshot: 100m);
        store.Seed(CreateCoupon("SAVE10", CouponDiscountType.Percentage, 10m));
        var service = CreateService(store);

        var result = await service.ValidateCouponAsync(new ValidateCheckoutCouponRequest
        {
            SessionId = "session-1",
            CouponCode = " save10 "
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("SAVE10", result.Value.CouponCode);
        Assert.Equal(200m, result.Value.Subtotal);
        Assert.Equal(200m, result.Value.EligibleSubtotal);
        Assert.Equal(20m, result.Value.DiscountAmount);
        Assert.Equal(180m, result.Value.TotalAmount);
        Assert.Equal(0, store.Coupons.Single().UsedCount);
    }

    [Fact]
    public async Task ValidateCouponAsync_TargetedCouponWithoutEligibleItems_ReturnsValidation()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store);
        SeedCart(store, variant.Id);
        var coupon = CreateCoupon("TARGET10", CouponDiscountType.Percentage, 10m);
        store.Seed(coupon);
        store.Seed(new CouponProductTarget(Guid.NewGuid(), coupon.Id, Guid.NewGuid()));
        var service = CreateService(store);

        var result = await service.ValidateCouponAsync(new ValidateCheckoutCouponRequest
        {
            SessionId = "session-1",
            CouponCode = "TARGET10"
        });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("Coupon does not apply to items in cart.", result.Errors);
    }

    [Fact]
    public async Task CheckoutAsync_WithCoupon_AppliesDiscountStoresSnapshotAndCreatesRedemption()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store);
        SeedCart(store, variant.Id, quantity: 2, unitPriceSnapshot: 100m);
        var coupon = CreateCoupon("SAVE25", CouponDiscountType.FixedAmount, 25m);
        store.Seed(coupon);
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest(couponCode: "save25"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var order = result.Value.Order;
        Assert.Equal(coupon.Id, order.CouponId);
        Assert.Equal("SAVE25", order.CouponCodeSnapshot);
        Assert.Equal("Save coupon", order.CouponNameSnapshot);
        Assert.Equal(25m, order.DiscountAmount);
        Assert.Equal(175m, order.TotalAmount);
        Assert.Equal(1, coupon.UsedCount);
        var redemption = Assert.Single(store.CouponRedemptions);
        Assert.Equal(coupon.Id, redemption.CouponId);
        Assert.Equal(store.Orders.Single().Id, redemption.OrderId);
        Assert.Equal("SAVE25", redemption.CodeSnapshot);
    }

    [Fact]
    public async Task CheckoutAsync_CouponUsageLimitReached_ReturnsConflict()
    {
        var store = new FakeCheckoutStore();
        var variant = SeedVisibleVariant(store);
        SeedCart(store, variant.Id);
        var coupon = CreateCoupon("LIMIT1", CouponDiscountType.Percentage, 10m, usageLimit: 1);
        coupon.ReserveUsage();
        store.Seed(coupon);
        var service = CreateService(store);

        var result = await service.CheckoutAsync(CreateRequest(couponCode: "LIMIT1"));

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Coupon usage limit has been reached.", result.Errors);
        Assert.Empty(store.Orders);
        Assert.Empty(store.CouponRedemptions);
    }

    private static CheckoutService CreateService(FakeCheckoutStore store, Guid? customerId = null)
    {
        return new CheckoutService(
            store,
            new StubCurrentCustomerContext(customerId),
            new StubCurrentLanguageProvider(),
            new FakeShipmentService(),
            NullLogger<CheckoutService>.Instance,
            new CheckoutRequestValidator(),
            new ValidateCheckoutCouponRequestValidator());
    }

    private static CheckoutRequest CreateRequest(
        PaymentMethod paymentMethod = PaymentMethod.Cod,
        string? couponCode = null)
    {
        return new CheckoutRequest
        {
            SessionId = "session-1",
            CustomerName = "Nguyen Van A",
            CustomerPhone = "0900000000",
            CustomerEmail = "customer@example.com",
            ShippingAddress = "123 Shipping Street, Ward 1, Ho Chi Minh",
            ShippingStreet = "123 Shipping Street",
            ShippingWard = "Ward 1",
            ShippingProvince = "Ho Chi Minh",
            Note = "Call before delivery",
            PaymentMethod = paymentMethod,
            CouponCode = couponCode
        };
    }

    private static void SeedCart(
        FakeCheckoutStore store,
        Guid variantId,
        int quantity = 1,
        decimal unitPriceSnapshot = 100m)
    {
        var cart = new CartAggregate(Guid.NewGuid(), null, "session-1");
        cart.AddItem(Guid.NewGuid(), variantId, quantity, unitPriceSnapshot);
        store.Seed(cart);
    }

    private static ProductVariant SeedVisibleVariant(
        FakeCheckoutStore store,
        decimal price = 100m,
        int stockQuantity = 10,
        bool requiresInstallation = false,
        bool isCategoryActive = true,
        bool isProductActive = true,
        bool isVariantActive = true)
    {
        var category = new Category(Guid.NewGuid(), null, LocalizedText.Of("Desks"), "desks", 1, isCategoryActive);
        var product = new Product(Guid.NewGuid(), category.Id, LocalizedText.Of("Standing Desk"), "standing-desk", LocalizedText.Of("Description"), false, isProductActive);
        var variant = new ProductVariant(
            Guid.NewGuid(),
            product.Id,
            "DESK-001",
            "Default",
            null,
            null,
            price,
            null,
            stockQuantity,
            requiresInstallation,
            isVariantActive);

        store.Seed(category);
        store.Seed(product);
        store.Seed(variant);

        return variant;
    }

    private static Coupon CreateCoupon(
        string code,
        CouponDiscountType discountType,
        decimal discountValue,
        int? usageLimit = null)
    {
        return new Coupon(
            Guid.NewGuid(),
            code,
            "Save coupon",
            null,
            discountType,
            discountValue,
            null,
            null,
            null,
            null,
            usageLimit);
    }

    private sealed class StubCurrentCustomerContext(Guid? customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;

        public string? Email => customerId.HasValue ? "customer@example.com" : null;
    }

    private sealed class StubCurrentLanguageProvider : ICurrentLanguageProvider
    {
        public string CurrentLanguage => "en";
    }

    private sealed class FakeShipmentService : IShipmentService
    {
        public Task<ShippingQuoteResponse> GetShippingQuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShippingQuoteResponse
            {
                TotalFeeAmount = 0m,
                BaseFeeAmount = 0m,
                ExtraWeightFeeAmount = 0m,
                InsuranceFeeAmount = 0m,
                RouteType = "Standard",
                Currency = "VND"
            });
        }

        public Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateShipmentResponse
            {
                ShipmentId = Guid.NewGuid(),
                ExternalOrderId = request.ExternalOrderId,
                TrackingCode = "ML-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
                Status = "PendingPickup",
                ShippingFeeAmount = 0m,
                Currency = "VND"
            });
        }

        public Task<TrackingResponse> GetTrackingAsync(string trackingCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TrackingResponse
            {
                TrackingCode = trackingCode,
                ExternalOrderId = "ECOM-1001",
                Status = "PendingPickup",
                ShippingFeeAmount = 0m,
                Timeline = []
            });
        }
    }
}
