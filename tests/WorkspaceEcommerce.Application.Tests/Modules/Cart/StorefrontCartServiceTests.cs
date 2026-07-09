using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Cart;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Tests.Modules.Cart;

public sealed class StorefrontCartServiceTests
{
    [Fact]
    public async Task GetCartAsync_MissingCart_ReturnsEmptyCartWithoutSaving()
    {
        var store = new FakeCartStore();
        var service = CreateService(store);

        var result = await service.GetCartAsync(new GetCartRequest { SessionId = "session-1" });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(Guid.Empty, result.Value.Id);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, store.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddItemAsync_ActiveVariant_CreatesCartWithPriceSnapshot()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store, price: 150m, stockQuantity: 5);
        var service = CreateService(store);

        var result = await service.AddItemAsync(new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = variant.Id,
            Quantity = 2
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(variant.Id, item.ProductVariantId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(150m, item.UnitPriceSnapshot);
        Assert.Equal(300m, result.Value.TotalAmount);
        Assert.Equal(1, store.SaveChangesCallCount);
    }

    [Fact]
    public async Task AddItemAsync_SameVariant_IncreasesQuantityAndKeepsSnapshot()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store, price: 100m, stockQuantity: 5);
        var cart = new CartAggregate(Guid.NewGuid(), null, "session-1");
        cart.AddItem(Guid.NewGuid(), variant.Id, 1, 90m);
        store.Seed(cart);
        var service = CreateService(store);

        var result = await service.AddItemAsync(new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = variant.Id,
            Quantity = 2
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(90m, item.UnitPriceSnapshot);
        Assert.Equal(270m, result.Value.TotalAmount);
    }

    [Fact]
    public async Task AddItemAsync_InactiveVariant_ReturnsNotFound()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store, isVariantActive: false);
        var service = CreateService(store);

        var result = await service.AddItemAsync(new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = variant.Id,
            Quantity = 1
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Product variant was not found.", result.Errors);
    }

    [Fact]
    public async Task AddItemAsync_InactiveProductOrCategory_ReturnsNotFound()
    {
        var inactiveProductStore = new FakeCartStore();
        var inactiveProductVariant = SeedVisibleVariant(inactiveProductStore, isProductActive: false);
        var inactiveCategoryStore = new FakeCartStore();
        var inactiveCategoryVariant = SeedVisibleVariant(inactiveCategoryStore, isCategoryActive: false);

        var inactiveProductResult = await CreateService(inactiveProductStore).AddItemAsync(new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = inactiveProductVariant.Id,
            Quantity = 1
        });
        var inactiveCategoryResult = await CreateService(inactiveCategoryStore).AddItemAsync(new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = inactiveCategoryVariant.Id,
            Quantity = 1
        });

        Assert.Equal(ResultStatus.NotFound, inactiveProductResult.Status);
        Assert.Equal(ResultStatus.NotFound, inactiveCategoryResult.Status);
    }

    [Fact]
    public async Task AddItemAsync_QuantityExceedsStock_ReturnsConflict()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store, stockQuantity: 1);
        var service = CreateService(store);

        var result = await service.AddItemAsync(new AddCartItemRequest
        {
            SessionId = "session-1",
            ProductVariantId = variant.Id,
            Quantity = 2
        });

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Requested quantity exceeds available stock.", result.Errors);
    }

    [Fact]
    public async Task UpdateItemAsync_ExistingItem_UpdatesQuantity()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store, stockQuantity: 5);
        var cart = new CartAggregate(Guid.NewGuid(), null, "session-1");
        var item = cart.AddItem(Guid.NewGuid(), variant.Id, 1, 100m);
        store.Seed(cart);
        var service = CreateService(store);

        var result = await service.UpdateItemAsync(item.Id, new UpdateCartItemRequest
        {
            SessionId = "session-1",
            Quantity = 3
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, Assert.Single(result.Value.Items).Quantity);
        Assert.Equal(300m, result.Value.TotalAmount);
    }

    [Fact]
    public async Task UpdateItemAsync_ItemOutsideSession_ReturnsNotFound()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store);
        var cart = new CartAggregate(Guid.NewGuid(), null, "session-1");
        var item = cart.AddItem(Guid.NewGuid(), variant.Id, 1, 100m);
        store.Seed(cart);
        var service = CreateService(store);

        var result = await service.UpdateItemAsync(item.Id, new UpdateCartItemRequest
        {
            SessionId = "other-session",
            Quantity = 2
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Cart item was not found.", result.Errors);
    }

    [Fact]
    public async Task RemoveItemAsync_ExistingItem_RemovesFromCart()
    {
        var store = new FakeCartStore();
        var variant = SeedVisibleVariant(store);
        var cart = new CartAggregate(Guid.NewGuid(), null, "session-1");
        var item = cart.AddItem(Guid.NewGuid(), variant.Id, 1, 100m);
        store.Seed(cart);
        var service = CreateService(store);

        var result = await service.RemoveItemAsync(item.Id, new RemoveCartItemRequest { SessionId = "session-1" });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0m, result.Value.TotalAmount);
    }

    private static StorefrontCartService CreateService(FakeCartStore store)
    {
        return new StorefrontCartService(
            store,
            new StubCurrentLanguageProvider(),
            new GetCartRequestValidator(),
            new AddCartItemRequestValidator(),
            new UpdateCartItemRequestValidator(),
            new RemoveCartItemRequestValidator());
    }

    private static ProductVariant SeedVisibleVariant(
        FakeCartStore store,
        decimal price = 100m,
        int stockQuantity = 10,
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
            requiresInstallation: false,
            isVariantActive);

        store.Seed(category);
        store.Seed(product);
        store.Seed(variant);

        return variant;
    }

    private sealed class StubCurrentLanguageProvider : WorkspaceEcommerce.Application.Common.Localization.ICurrentLanguageProvider
    {
        public string CurrentLanguage => "en";
    }
}
