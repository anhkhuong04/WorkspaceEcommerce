using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

internal static class TestData
{
    public static CatalogSeed CreateVisibleCatalog()
    {
        var category = new Category(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            null,
            "Desks",
            "desks",
            1,
            isActive: true);
        var product = new Product(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            category.Id,
            "Standing Desk",
            "standing-desk",
            "A desk for focused work.",
            isFeatured: true,
            isActive: true);
        var variant = new ProductVariant(
            Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            product.Id,
            "DESK-001",
            "Default",
            "Black",
            "120cm",
            123.45m,
            150m,
            10,
            requiresInstallation: false,
            isActive: true);
        var image = product.AddImage(
            Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            "https://example.test/standing-desk.jpg",
            "Standing desk",
            1);
        var specification = product.AddSpecification(
            Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            "Material",
            "Wood",
            1);

        return new CatalogSeed(category, product, variant, image, specification);
    }

    public static Order CreatePendingOrder(Guid productVariantId)
    {
        var order = new Order(
            Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
            "ORD-TEST-0001",
            null,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod);

        order.AddItem(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            productVariantId,
            "Standing Desk",
            "DESK-001",
            100m,
            2,
            requiresInstallation: false);
        order.RecordCreated(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Created by checkout.",
            changedBy: null);

        return order;
    }
}

internal sealed record CatalogSeed(
    Category Category,
    Product Product,
    ProductVariant Variant,
    ProductImage Image,
    ProductSpecification Specification);
