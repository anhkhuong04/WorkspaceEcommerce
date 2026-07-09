using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Domain.Catalog;

public sealed class ProductTests
{
    [Fact]
    public void AddVariant_NewSku_AddsVariant()
    {
        var product = CreateProduct();

        var variant = product.AddVariant(
            Guid.NewGuid(),
            "SKU-001",
            "Black Desk",
            "Black",
            "120x60",
            1200000m,
            1500000m,
            10,
            requiresInstallation: true);

        Assert.Single(product.Variants);
        Assert.Equal(variant.Id, product.Variants.Single().Id);
    }

    [Fact]
    public void AddVariant_DuplicateSkuIgnoringCase_ThrowsDomainException()
    {
        var product = CreateProduct();
        product.AddVariant(
            Guid.NewGuid(),
            "SKU-001",
            "Black Desk",
            "Black",
            "120x60",
            1200000m,
            null,
            10,
            requiresInstallation: false);

        var exception = Assert.Throws<DomainException>(() => product.AddVariant(
            Guid.NewGuid(),
            "sku-001",
            "White Desk",
            "White",
            "120x60",
            1200000m,
            null,
            5,
            requiresInstallation: false));

        Assert.Equal("Product variant SKU must be unique within a product.", exception.Message);
    }

    private static Product CreateProduct()
    {
        return new Product(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new WorkspaceEcommerce.Domain.Common.LocalizedText { { "en", "Standing Desk" } },
            "standing-desk",
            new WorkspaceEcommerce.Domain.Common.LocalizedText { { "en", "Adjustable standing desk" } });
    }
}
