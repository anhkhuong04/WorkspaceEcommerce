using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;
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
        Assert.Equal(1, store.SaveChangesCallCount);
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

    private static CheckoutService CreateService(FakeCheckoutStore store)
    {
        return new CheckoutService(store, new CheckoutRequestValidator());
    }

    private static CheckoutRequest CreateRequest(PaymentMethod paymentMethod = PaymentMethod.Cod)
    {
        return new CheckoutRequest
        {
            SessionId = "session-1",
            CustomerName = "Nguyen Van A",
            CustomerPhone = "0900000000",
            CustomerEmail = "customer@example.com",
            ShippingAddress = "123 Shipping Street",
            Note = "Call before delivery",
            PaymentMethod = paymentMethod
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
        var category = new Category(Guid.NewGuid(), null, "Desks", "desks", 1, isCategoryActive);
        var product = new Product(Guid.NewGuid(), category.Id, "Standing Desk", "standing-desk", "Description", false, isProductActive);
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
}
