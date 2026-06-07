using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Domain.Catalog;

public sealed class ProductVariantTests
{
    [Fact]
    public void Constructor_NegativePrice_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateVariant(price: -1m));

        Assert.Equal("Price cannot be negative.", exception.Message);
    }

    [Fact]
    public void Constructor_CompareAtPriceLowerThanPrice_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateVariant(price: 100m, compareAtPrice: 90m));

        Assert.Equal("Compare-at price cannot be lower than price.", exception.Message);
    }

    [Fact]
    public void Constructor_NegativeStock_ThrowsDomainException()
    {
        var exception = Assert.Throws<DomainException>(() => CreateVariant(stockQuantity: -1));

        Assert.Equal("StockQuantity cannot be negative.", exception.Message);
    }

    [Fact]
    public void UpdatePricing_ValidPrice_UpdatesPriceAndCompareAtPrice()
    {
        var variant = CreateVariant(price: 100m, compareAtPrice: 120m);

        variant.UpdatePricing(110m, 150m);

        Assert.Equal(110m, variant.Price);
        Assert.Equal(150m, variant.CompareAtPrice);
    }

    [Fact]
    public void UpdatePricing_CompareAtPriceLowerThanPrice_ThrowsDomainException()
    {
        var variant = CreateVariant(price: 100m, compareAtPrice: 120m);

        var exception = Assert.Throws<DomainException>(() => variant.UpdatePricing(100m, 99m));

        Assert.Equal("Compare-at price cannot be lower than price.", exception.Message);
    }

    [Fact]
    public void UpdateStock_ValidQuantity_UpdatesStockQuantity()
    {
        var variant = CreateVariant(stockQuantity: 1);

        variant.UpdateStock(5);

        Assert.Equal(5, variant.StockQuantity);
    }

    [Fact]
    public void UpdateStock_NegativeQuantity_ThrowsDomainException()
    {
        var variant = CreateVariant(stockQuantity: 1);

        var exception = Assert.Throws<DomainException>(() => variant.UpdateStock(-1));

        Assert.Equal("StockQuantity cannot be negative.", exception.Message);
    }

    private static ProductVariant CreateVariant(
        decimal price = 100m,
        decimal? compareAtPrice = 120m,
        int stockQuantity = 10)
    {
        return new ProductVariant(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "SKU-001",
            "Black Desk",
            "Black",
            "120x60",
            price,
            compareAtPrice,
            stockQuantity,
            requiresInstallation: false);
    }
}
