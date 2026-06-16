using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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

    private const string HyperWorkCatalogUrl = "https://hyperwork.vn/en/collections/all/products.json?limit=250";
    private const string HyperWorkCategoryPrefix = "hyperwork-";
    private const string HyperWorkProductPrefix = "hyperwork-";
    private static readonly HttpClient HyperWorkHttpClient = CreateHyperWorkHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        var hyperWorkProducts = await FetchHyperWorkProductsAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var categories = await SeedCategoriesAsync(cancellationToken);
        var products = await SeedProductsAsync(cancellationToken);
        var variants = await SeedVariantsAsync(cancellationToken);
        await SeedProductContentAsync(cancellationToken);
        var hyperWorkResult = await SeedHyperWorkCatalogAsync(hyperWorkProducts, cancellationToken);
        var banners = await SeedBannersAsync(cancellationToken);
        var carts = await SeedCheckoutReadyCartAsync(cancellationToken);
        var orders = await SeedOrdersAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await RemoveObsoleteHyperWorkCategoriesAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DemoDataSeedResult(
            categories + hyperWorkResult.Categories,
            products + hyperWorkResult.Products,
            variants + hyperWorkResult.Variants,
            banners,
            carts,
            orders);
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
        var existingCategory = await dbContext.Categories
            .FirstOrDefaultAsync(category => category.Id == id || category.Slug == slug, cancellationToken);
        if (existingCategory is not null)
        {
            existingCategory.UpdateDetails(name, slug, sortOrder);
            existingCategory.MoveToParent(parentId);
            existingCategory.Activate();
            dbContext.Update(existingCategory);

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
        string? description,
        bool isFeatured,
        CancellationToken cancellationToken)
    {
        var existingProduct = await dbContext.Products
            .FirstOrDefaultAsync(product => product.Id == id || product.Slug == slug, cancellationToken);
        if (existingProduct is not null)
        {
            existingProduct.UpdateDetails(categoryId, name, slug, description);
            if (isFeatured)
            {
                existingProduct.MarkAsFeatured();
            }
            else
            {
                existingProduct.UnmarkAsFeatured();
            }

            existingProduct.Activate();
            dbContext.Update(existingProduct);

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
        var existingVariant = await dbContext.ProductVariants
            .FirstOrDefaultAsync(variant => variant.Id == id || variant.Sku == sku, cancellationToken);
        if (existingVariant is not null)
        {
            existingVariant.UpdateDetails(sku, name, color, size, requiresInstallation);
            existingVariant.UpdatePricing(price, compareAtPrice);
            existingVariant.UpdateStock(stockQuantity);
            existingVariant.Activate();
            dbContext.Update(existingVariant);

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
            var existingImage = await dbContext.ProductImages
                .FirstOrDefaultAsync(existing => existing.Id == image.Id, cancellationToken);
            if (existingImage is null)
            {
                dbContext.Add(image);
                continue;
            }

            existingImage.Update(image.ImageUrl, image.AltText, image.SortOrder);
            dbContext.Update(existingImage);
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
        var existingBanner = await dbContext.Banners
            .FirstOrDefaultAsync(banner => banner.Id == id, cancellationToken);
        if (existingBanner is not null)
        {
            existingBanner.UpdateDetails(title, imageUrl, linkUrl, sortOrder);
            existingBanner.Activate();
            dbContext.Update(existingBanner);

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

    private async Task<HyperWorkSeedResult> SeedHyperWorkCatalogAsync(
        IReadOnlyCollection<HyperWorkProduct> hyperWorkProducts,
        CancellationToken cancellationToken)
    {
        var categoryCount = 0;
        var productCount = 0;
        var variantCount = 0;
        var imageCount = 0;
        var categoryIdsBySlug = await GetCategoryIdsBySlugAsync(cancellationToken);
        var imageIdsInBatch = new HashSet<Guid>();
        var skusInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var hyperWorkProduct in hyperWorkProducts.Where(product => product.Variants.Count > 0))
        {
            var categoryId = ResolveHyperWorkCategoryId(hyperWorkProduct, categoryIdsBySlug);

            var productId = StableGuid("hyperwork-product", hyperWorkProduct.Id.ToString(CultureInfo.InvariantCulture));
            var productSlug = HyperWorkProductPrefix + Slugify(hyperWorkProduct.Handle);
            var description = NormalizeDescription(hyperWorkProduct.BodyHtml);
            productCount += await EnsureProductAsync(
                productId,
                categoryId,
                Truncate(hyperWorkProduct.Title, 250),
                Truncate(productSlug, 250),
                description,
                isFeatured: productCount < 8,
                cancellationToken);

            foreach (var hyperWorkVariant in hyperWorkProduct.Variants)
            {
                var price = ParseMoney(hyperWorkVariant.Price);
                if (price is null)
                {
                    continue;
                }

                var compareAtPrice = ParseMoney(hyperWorkVariant.CompareAtPrice);
                if (compareAtPrice is not null && compareAtPrice < price)
                {
                    compareAtPrice = null;
                }

                var baseSku = string.IsNullOrWhiteSpace(hyperWorkVariant.Sku)
                    ? $"HYPERWORK-{hyperWorkVariant.Id}"
                    : hyperWorkVariant.Sku.Trim();
                var sku = GetUniqueSku(baseSku, hyperWorkVariant.Id, skusInBatch);

                variantCount += await EnsureVariantAsync(
                    StableGuid("hyperwork-variant", hyperWorkVariant.Id.ToString(CultureInfo.InvariantCulture)),
                    productId,
                    sku,
                    Truncate(GetVariantName(hyperWorkProduct, hyperWorkVariant), 250),
                    TruncateOptional(NormalizeOption(hyperWorkVariant.Option1), 100),
                    TruncateOptional(NormalizeOption(hyperWorkVariant.Option2), 100),
                    price.Value,
                    compareAtPrice,
                    hyperWorkVariant.Available ? 25 : 0,
                    RequiresInstallation(hyperWorkProduct),
                    cancellationToken);
            }

            imageCount += await EnsureHyperWorkImagesAsync(productId, hyperWorkProduct, imageIdsInBatch, cancellationToken);
        }

        return new HyperWorkSeedResult(categoryCount, productCount, variantCount, imageCount);
    }

    private async Task<Dictionary<string, Guid>> GetCategoryIdsBySlugAsync(CancellationToken cancellationToken)
    {
        var categoryIdsBySlug = await dbContext.Categories
            .ToDictionaryAsync(category => category.Slug, category => category.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

        categoryIdsBySlug.TryAdd("standing-desks", DesksCategoryId);
        categoryIdsBySlug.TryAdd("ergonomic-chairs", ChairsCategoryId);
        categoryIdsBySlug.TryAdd("desk-accessories", AccessoriesCategoryId);

        return categoryIdsBySlug;
    }

    private async Task RemoveObsoleteHyperWorkCategoriesAsync(CancellationToken cancellationToken)
    {
        var obsoleteCategories = await dbContext.Categories
            .Where(category => category.Slug.StartsWith(HyperWorkCategoryPrefix) &&
                !dbContext.Products.Any(product => product.CategoryId == category.Id) &&
                !dbContext.Categories.Any(child => child.ParentId == category.Id))
            .ToListAsync(cancellationToken);

        dbContext.Categories.RemoveRange(obsoleteCategories);
    }

    private async Task<int> EnsureHyperWorkImagesAsync(
        Guid productId,
        HyperWorkProduct hyperWorkProduct,
        HashSet<Guid> imageIdsInBatch,
        CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var hyperWorkImage in hyperWorkProduct.Images.OrderBy(image => image.Position).Take(4))
        {
            if (string.IsNullOrWhiteSpace(hyperWorkImage.Src))
            {
                continue;
            }

            var imageId = StableGuid("hyperwork-image", hyperWorkImage.Id.ToString(CultureInfo.InvariantCulture));
            if (!imageIdsInBatch.Add(imageId))
            {
                continue;
            }

            var image = new ProductImage(
                imageId,
                productId,
                hyperWorkImage.Src,
                Truncate(hyperWorkProduct.Title, 250),
                hyperWorkImage.Position);
            var existingImage = await dbContext.ProductImages
                .FirstOrDefaultAsync(existing => existing.Id == image.Id, cancellationToken);
            if (existingImage is null)
            {
                dbContext.Add(image);
                count++;
                continue;
            }

            existingImage.Update(image.ImageUrl, image.AltText, image.SortOrder);
            dbContext.Update(existingImage);
        }

        return count;
    }

    private static async Task<IReadOnlyCollection<HyperWorkProduct>> FetchHyperWorkProductsAsync(CancellationToken cancellationToken)
    {
        using var response = await HyperWorkHttpClient.GetAsync(HyperWorkCatalogUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var catalog = await JsonSerializer.DeserializeAsync<HyperWorkCatalogResponse>(stream, JsonOptions, cancellationToken);

        return catalog?.Products
            .Where(product => !string.IsNullOrWhiteSpace(product.Title) && !string.IsNullOrWhiteSpace(product.Handle))
            .ToArray() ?? [];
    }

    private static HttpClient CreateHyperWorkHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json,text/plain,*/*");
        client.DefaultRequestHeaders.Referrer = new Uri("https://hyperwork.vn/en/collections/all");

        return client;
    }

    private static Guid ResolveHyperWorkCategoryId(
        HyperWorkProduct product,
        IReadOnlyDictionary<string, Guid> categoryIdsBySlug)
    {
        var searchText = GetHyperWorkSearchText(product);

        if (ContainsAny(searchText, "ban-nang-ha", "standing-desk", "khung-ban", "ban-van-phong", "core-desk", "ban-hyperwork-atlas"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "desk", "standing-desks", "desk-accessories");
        }

        if (ContainsAny(searchText, "ghe", "chair", "footrest"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "chair", "ergonomic-chairs", "desk-accessories");
        }

        if (ContainsAny(searchText, "tu-tai-lieu", "archive", "pegboard"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "archive", "desk-accessories");
        }

        if (ContainsAny(searchText, "ban-phim", "keyboard", "mouse", "chuot", "wrist-rest", "ke-tay", "macro", "core-click", "core-type", "silentium", "kb1", "phu-phim"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "keyboard-mouse", "desk-accessories");
        }

        if (ContainsAny(searchText, "den-man-hinh", "den-ban", "den-de-ban", "desk-lamp", "monitor-lamp"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "lighting-equipment", "desk-accessories");
        }

        if (ContainsAny(searchText, "monitor", "man-hinh", "laptop-stand", "gia-treo-laptop", "gia-do-latop", "gia-do-laptop", "tv-mount", "tvc", "headphone-stand", "tai-nghe", "microphone-stand", "shock-mount", "phone-tablet-stand"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "monitor-solutions", "desk-accessories");
        }

        if (ContainsAny(searchText, "magsnap", "wallet", "iphone", "vi-nam-cham"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "magsnap-wallet", "desk-accessories");
        }

        if (ContainsAny(searchText, "tumbler", "coc", "coaster", "cup-holder", "giu-nhiet"))
        {
            return ResolveCategoryId(categoryIdsBySlug, "tumbler", "desk-accessories");
        }

        return ResolveCategoryId(categoryIdsBySlug, "accessories", "desk-accessories");
    }

    private static string GetVariantName(HyperWorkProduct product, HyperWorkVariant variant)
    {
        if (string.IsNullOrWhiteSpace(variant.Title) ||
            string.Equals(variant.Title, "Default Title", StringComparison.OrdinalIgnoreCase))
        {
            return product.Title;
        }

        return $"{product.Title} - {variant.Title}";
    }

    private static string GetUniqueSku(string baseSku, long variantId, HashSet<string> skusInBatch)
    {
        var sku = Truncate(baseSku, 100);
        if (skusInBatch.Add(sku))
        {
            return sku;
        }

        var suffix = $"-{variantId}";
        var maxBaseLength = 100 - suffix.Length;
        sku = $"{Truncate(baseSku, maxBaseLength)}{suffix}";
        if (skusInBatch.Add(sku))
        {
            return sku;
        }

        var fallback = $"HYPERWORK-{variantId}";
        skusInBatch.Add(fallback);

        return fallback;
    }

    private static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static string? NormalizeDescription(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var withoutTags = Regex.Replace(WebUtility.HtmlDecode(html), "<.*?>", " ");
        var normalized = Regex.Replace(withoutTags, "\\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeOption(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "Default Title", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim();
    }

    private static Guid ResolveCategoryId(IReadOnlyDictionary<string, Guid> categoryIdsBySlug, params string[] slugs)
    {
        foreach (var slug in slugs)
        {
            if (categoryIdsBySlug.TryGetValue(slug, out var categoryId))
            {
                return categoryId;
            }
        }

        return AccessoriesCategoryId;
    }

    private static bool ContainsAny(string value, params string[] patterns)
    {
        return patterns.Any(pattern => value.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetHyperWorkSearchText(HyperWorkProduct product)
    {
        var text = string.Join(
            ' ',
            product.Title,
            product.Handle,
            product.ProductType,
            product.Vendor);

        return Slugify(text).Replace('đ', 'd');
    }

    private static bool RequiresInstallation(HyperWorkProduct product)
    {
        var searchText = GetHyperWorkSearchText(product);

        return ContainsAny(searchText, "standing-desk", "ban-nang-ha", "monitor-stand", "tv-mount", "tvc");
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastWasDash = false;

        foreach (var character in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(character);
            if (unicodeCategory == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(lower))
            {
                builder.Append(lower);
                lastWasDash = false;
                continue;
            }

            if (!lastWasDash)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        var slug = builder.ToString().Trim('-');

        return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
    }

    private static Guid StableGuid(string scope, string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes($"{scope}:{value}"));
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x30);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? TruncateOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
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

    private sealed record HyperWorkSeedResult(int Categories, int Products, int Variants, int Images);

    private sealed record HyperWorkCatalogResponse(
        [property: JsonPropertyName("products")] IReadOnlyCollection<HyperWorkProduct> Products);

    private sealed record HyperWorkProduct(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("handle")] string Handle,
        [property: JsonPropertyName("body_html")] string? BodyHtml,
        [property: JsonPropertyName("vendor")] string? Vendor,
        [property: JsonPropertyName("product_type")] string? ProductType,
        [property: JsonPropertyName("variants")] IReadOnlyCollection<HyperWorkVariant> Variants,
        [property: JsonPropertyName("images")] IReadOnlyCollection<HyperWorkImage> Images);

    private sealed record HyperWorkVariant(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("option1")] string? Option1,
        [property: JsonPropertyName("option2")] string? Option2,
        [property: JsonPropertyName("sku")] string? Sku,
        [property: JsonPropertyName("available")] bool Available,
        [property: JsonPropertyName("price")] string? Price,
        [property: JsonPropertyName("compare_at_price")] string? CompareAtPrice);

    private sealed record HyperWorkImage(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("position")] int Position,
        [property: JsonPropertyName("src")] string? Src);
}
