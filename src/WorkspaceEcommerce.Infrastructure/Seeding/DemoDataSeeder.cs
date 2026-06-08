using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Seeding;
using WorkspaceEcommerce.Domain.Modules.Cart;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Seeding;

internal sealed class DemoDataSeeder(AppDbContext dbContext) : IDemoDataSeeder
{
    public const string CheckoutReadySessionId = "demo-checkout-session";

    private static readonly Guid DesksCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ChairsCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid AccessoriesCategoryId = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private static readonly Guid StandingDeskProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ChairProductId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid MonitorArmProductId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly Guid DeskLampProductId = Guid.Parse("20000000-0000-0000-0000-000000000004");

    private static readonly Guid StandingDeskOakVariantId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid StandingDeskBlackVariantId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid ChairVariantId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static readonly Guid MonitorArmVariantId = Guid.Parse("30000000-0000-0000-0000-000000000004");
    private static readonly Guid DeskLampVariantId = Guid.Parse("30000000-0000-0000-0000-000000000005");

    public async Task<DemoDataSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var categories = await SeedCategoriesAsync(cancellationToken);
        var products = await SeedProductsAsync(cancellationToken);
        var variants = await SeedVariantsAsync(cancellationToken);
        await SeedProductContentAsync(cancellationToken);
        var banners = await SeedBannersAsync(cancellationToken);
        var carts = await SeedCheckoutReadyCartAsync(cancellationToken);
        var orders = await SeedOrdersAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DemoDataSeedResult(categories, products, variants, banners, carts, orders);
    }

    private async Task<int> SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        count += await EnsureCategoryAsync(DesksCategoryId, null, "Standing Desks", "standing-desks", 1, cancellationToken);
        count += await EnsureCategoryAsync(ChairsCategoryId, null, "Ergonomic Chairs", "ergonomic-chairs", 2, cancellationToken);
        count += await EnsureCategoryAsync(AccessoriesCategoryId, null, "Desk Accessories", "desk-accessories", 3, cancellationToken);

        return count;
    }

    private async Task<int> EnsureCategoryAsync(
        Guid id,
        Guid? parentId,
        string name,
        string slug,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Categories.AnyAsync(category => category.Slug == slug, cancellationToken))
        {
            return 0;
        }

        dbContext.Add(new Category(id, parentId, name, slug, sortOrder, isActive: true));

        return 1;
    }

    private async Task<int> SeedProductsAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        count += await EnsureProductAsync(
            StandingDeskProductId,
            DesksCategoryId,
            "Atlas Standing Desk",
            "atlas-standing-desk",
            "Height-adjustable desk for a clean workstation.",
            isFeatured: true,
            cancellationToken);
        count += await EnsureProductAsync(
            ChairProductId,
            ChairsCategoryId,
            "Forma Ergonomic Chair",
            "forma-ergonomic-chair",
            "Supportive task chair for full-day work.",
            isFeatured: true,
            cancellationToken);
        count += await EnsureProductAsync(
            MonitorArmProductId,
            AccessoriesCategoryId,
            "Axis Dual Monitor Arm",
            "axis-dual-monitor-arm",
            "Dual monitor arm for flexible screen positioning.",
            isFeatured: true,
            cancellationToken);
        count += await EnsureProductAsync(
            DeskLampProductId,
            AccessoriesCategoryId,
            "Halo Desk Lamp",
            "halo-desk-lamp",
            "Warm LED desk lamp for focused evening work.",
            isFeatured: false,
            cancellationToken);

        return count;
    }

    private async Task<int> EnsureProductAsync(
        Guid id,
        Guid categoryId,
        string name,
        string slug,
        string description,
        bool isFeatured,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Products.AnyAsync(product => product.Slug == slug, cancellationToken))
        {
            return 0;
        }

        dbContext.Add(new Product(id, categoryId, name, slug, description, isFeatured, isActive: true));

        return 1;
    }

    private async Task<int> SeedVariantsAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        count += await EnsureVariantAsync(StandingDeskOakVariantId, StandingDeskProductId, "DEMO-DESK-OAK-140", "Oak / 140cm", "Oak", "140cm", 699m, 799m, 18, true, cancellationToken);
        count += await EnsureVariantAsync(StandingDeskBlackVariantId, StandingDeskProductId, "DEMO-DESK-BLK-160", "Black / 160cm", "Black", "160cm", 749m, 849m, 4, true, cancellationToken);
        count += await EnsureVariantAsync(ChairVariantId, ChairProductId, "DEMO-CHAIR-GRAPHITE", "Graphite", "Graphite", null, 329m, 399m, 12, false, cancellationToken);
        count += await EnsureVariantAsync(MonitorArmVariantId, MonitorArmProductId, "DEMO-ARM-DUAL", "Dual arm", "Matte Black", null, 189m, 229m, 3, false, cancellationToken);
        count += await EnsureVariantAsync(DeskLampVariantId, DeskLampProductId, "DEMO-LAMP-WARM", "Warm light", "White", null, 79m, 99m, 25, false, cancellationToken);

        return count;
    }

    private async Task<int> EnsureVariantAsync(
        Guid id,
        Guid productId,
        string sku,
        string name,
        string? color,
        string? size,
        decimal price,
        decimal? compareAtPrice,
        int stockQuantity,
        bool requiresInstallation,
        CancellationToken cancellationToken)
    {
        if (await dbContext.ProductVariants.AnyAsync(variant => variant.Sku == sku, cancellationToken))
        {
            return 0;
        }

        dbContext.Add(new ProductVariant(id, productId, sku, name, color, size, price, compareAtPrice, stockQuantity, requiresInstallation, isActive: true));

        return 1;
    }

    private async Task SeedProductContentAsync(CancellationToken cancellationToken)
    {
        await EnsureProductImagesAsync(cancellationToken);
        await EnsureProductSpecificationsAsync(cancellationToken);
    }

    private async Task EnsureProductImagesAsync(CancellationToken cancellationToken)
    {
        var images = new[]
        {
            new ProductImage(Guid.Parse("40000000-0000-0000-0000-000000000001"), StandingDeskProductId, "/demo/atlas-standing-desk-1.png", "Atlas standing desk", 1),
            new ProductImage(Guid.Parse("40000000-0000-0000-0000-000000000002"), ChairProductId, "/demo/forma-chair-1.png", "Forma ergonomic chair", 1),
            new ProductImage(Guid.Parse("40000000-0000-0000-0000-000000000003"), MonitorArmProductId, "/demo/axis-monitor-arm-1.png", "Axis dual monitor arm", 1),
            new ProductImage(Guid.Parse("40000000-0000-0000-0000-000000000004"), DeskLampProductId, "/demo/halo-desk-lamp-1.png", "Halo desk lamp", 1)
        };

        foreach (var image in images)
        {
            if (!await dbContext.ProductImages.AnyAsync(existing => existing.Id == image.Id, cancellationToken))
            {
                dbContext.Add(image);
            }
        }
    }

    private async Task EnsureProductSpecificationsAsync(CancellationToken cancellationToken)
    {
        var specifications = new[]
        {
            new ProductSpecification(Guid.Parse("50000000-0000-0000-0000-000000000001"), StandingDeskProductId, "Frame", "Dual motor adjustable frame", 1),
            new ProductSpecification(Guid.Parse("50000000-0000-0000-0000-000000000002"), StandingDeskProductId, "Warranty", "5 years", 2),
            new ProductSpecification(Guid.Parse("50000000-0000-0000-0000-000000000003"), ChairProductId, "Adjustment", "4D armrest and lumbar support", 1),
            new ProductSpecification(Guid.Parse("50000000-0000-0000-0000-000000000004"), MonitorArmProductId, "Capacity", "Up to 9kg per arm", 1),
            new ProductSpecification(Guid.Parse("50000000-0000-0000-0000-000000000005"), DeskLampProductId, "Light", "Three temperature modes", 1)
        };

        foreach (var specification in specifications)
        {
            if (!await dbContext.ProductSpecifications.AnyAsync(existing => existing.Id == specification.Id, cancellationToken))
            {
                dbContext.Add(specification);
            }
        }
    }

    private async Task<int> SeedBannersAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        count += await EnsureBannerAsync(Guid.Parse("60000000-0000-0000-0000-000000000001"), "Build a calmer workspace", "/demo/banner-workspace.svg", "/products?categorySlug=standing-desks", 1, cancellationToken);
        count += await EnsureBannerAsync(Guid.Parse("60000000-0000-0000-0000-000000000002"), "Ergonomic essentials", "/demo/banner-ergonomic.png", "/products?categorySlug=ergonomic-chairs", 2, cancellationToken);
        count += await EnsureBannerAsync(Guid.Parse("60000000-0000-0000-0000-000000000003"), "Accessories for focus", "/demo/banner-accessories.png", "/products?categorySlug=desk-accessories", 3, cancellationToken);

        return count;
    }

    private async Task<int> EnsureBannerAsync(
        Guid id,
        string title,
        string imageUrl,
        string linkUrl,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Banners.AnyAsync(banner => banner.Title == title, cancellationToken))
        {
            return 0;
        }

        dbContext.Add(new Banner(id, title, imageUrl, linkUrl, sortOrder, isActive: true));

        return 1;
    }

    private async Task<int> SeedCheckoutReadyCartAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Carts.AnyAsync(cart => cart.SessionId == CheckoutReadySessionId, cancellationToken))
        {
            return 0;
        }

        var cart = new Cart(Guid.Parse("70000000-0000-0000-0000-000000000001"), null, CheckoutReadySessionId);
        cart.AddItem(Guid.Parse("71000000-0000-0000-0000-000000000001"), StandingDeskOakVariantId, 1, 699m);
        cart.AddItem(Guid.Parse("71000000-0000-0000-0000-000000000002"), DeskLampVariantId, 2, 79m);
        dbContext.Add(cart);

        return 1;
    }

    private async Task<int> SeedOrdersAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        count += await EnsureOrderAsync(CreatePendingOrder(), cancellationToken);
        count += await EnsureOrderAsync(CreateConfirmedOrder(), cancellationToken);
        count += await EnsureOrderAsync(CreateCompletedOrder(), cancellationToken);

        return count;
    }

    private async Task<int> EnsureOrderAsync(Order order, CancellationToken cancellationToken)
    {
        if (await dbContext.Orders.AnyAsync(existing => existing.OrderCode == order.OrderCode, cancellationToken))
        {
            return 0;
        }

        dbContext.Add(order);

        return 1;
    }

    private static Order CreatePendingOrder()
    {
        var order = new Order(
            Guid.Parse("80000000-0000-0000-0000-000000000001"),
            "ORD-DEMO-PENDING",
            null,
            "Nguyen Minh Anh",
            "0900000001",
            "minhanh@example.test",
            "12 Nguyen Trai, District 1, Ho Chi Minh City",
            "Demo pending order",
            PaymentMethod.Cod);
        order.AddItem(Guid.Parse("81000000-0000-0000-0000-000000000001"), StandingDeskOakVariantId, "Atlas Standing Desk", "DEMO-DESK-OAK-140", 699m, 1, true);
        order.RecordCreated(Guid.Parse("82000000-0000-0000-0000-000000000001"), "Created by demo seed.", null);

        return order;
    }

    private static Order CreateConfirmedOrder()
    {
        var order = new Order(
            Guid.Parse("80000000-0000-0000-0000-000000000002"),
            "ORD-DEMO-CONFIRMED",
            null,
            "Tran Quoc Bao",
            "0900000002",
            "quocbao@example.test",
            "88 Dien Bien Phu, Binh Thanh, Ho Chi Minh City",
            "Demo confirmed order",
            PaymentMethod.ManualBankTransfer);
        order.AddItem(Guid.Parse("81000000-0000-0000-0000-000000000002"), ChairVariantId, "Forma Ergonomic Chair", "DEMO-CHAIR-GRAPHITE", 329m, 1, false);
        order.RecordCreated(Guid.Parse("82000000-0000-0000-0000-000000000002"), "Created by demo seed.", null);
        order.ChangeStatus(Guid.Parse("82000000-0000-0000-0000-000000000003"), OrderStatus.Confirmed, "Confirmed by demo seed.", "admin@example.com");

        return order;
    }

    private static Order CreateCompletedOrder()
    {
        var order = new Order(
            Guid.Parse("80000000-0000-0000-0000-000000000003"),
            "ORD-DEMO-COMPLETED",
            null,
            "Le Hoang Nam",
            "0900000003",
            "hoangnam@example.test",
            "25 Le Loi, District 3, Ho Chi Minh City",
            "Demo completed order",
            PaymentMethod.Cod);
        order.AddItem(Guid.Parse("81000000-0000-0000-0000-000000000003"), MonitorArmVariantId, "Axis Dual Monitor Arm", "DEMO-ARM-DUAL", 189m, 2, false);
        order.AddItem(Guid.Parse("81000000-0000-0000-0000-000000000004"), DeskLampVariantId, "Halo Desk Lamp", "DEMO-LAMP-WARM", 79m, 1, false);
        order.RecordCreated(Guid.Parse("82000000-0000-0000-0000-000000000004"), "Created by demo seed.", null);
        order.ChangeStatus(Guid.Parse("82000000-0000-0000-0000-000000000005"), OrderStatus.Confirmed, "Confirmed by demo seed.", "admin@example.com");
        order.ChangeStatus(Guid.Parse("82000000-0000-0000-0000-000000000006"), OrderStatus.Processing, "Processing by demo seed.", "admin@example.com");
        order.ChangeStatus(Guid.Parse("82000000-0000-0000-0000-000000000007"), OrderStatus.Shipping, "Shipping by demo seed.", "admin@example.com");
        order.ChangeStatus(Guid.Parse("82000000-0000-0000-0000-000000000008"), OrderStatus.Completed, "Completed by demo seed.", "admin@example.com");

        return order;
    }
}
