using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using CartAggregate = WorkspaceEcommerce.Domain.Modules.Cart.Cart;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutCartBuilder(
    ICheckoutStore checkoutStore,
    ICurrentLanguageProvider languageProvider)
{
    public async Task<Result<IReadOnlyCollection<CheckoutItemSnapshot>>> BuildItemSnapshotsAsync(
        CartAggregate cart,
        bool lockVariants,
        CancellationToken cancellationToken)
    {
        var cartItems = cart.Items.ToArray();
        var variantIds = cartItems
            .Select(item => item.ProductVariantId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var variants = lockVariants
            ? await checkoutStore.FindProductVariantsForUpdateAsync(variantIds, cancellationToken)
            : await checkoutStore.FindProductVariantsByIdsAsync(variantIds, cancellationToken);
        var variantsById = variants.ToDictionary(variant => variant.Id);
        var productIds = variants
            .Select(variant => variant.ProductId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var products = await checkoutStore.FindProductsByIdsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);
        var categoryIds = products
            .Select(product => product.CategoryId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();
        var categoriesById = (await checkoutStore.FindCategoriesByIdsAsync(categoryIds, cancellationToken))
            .ToDictionary(category => category.Id);
        var snapshots = new List<CheckoutItemSnapshot>();

        foreach (var cartItem in cartItems)
        {
            if (!variantsById.TryGetValue(cartItem.ProductVariantId, out var variant) || !variant.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            if (!productsById.TryGetValue(variant.ProductId, out var product) || !product.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            if (!categoriesById.TryGetValue(product.CategoryId, out var category) || !category.IsActive)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.NotFound("Product variant was not found.");
            }

            if (cartItem.Quantity > variant.StockQuantity)
            {
                return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.Conflict("Requested quantity exceeds available stock.");
            }

            snapshots.Add(new CheckoutItemSnapshot(
                variant,
                product.Id,
                product.Name.Get(languageProvider.CurrentLanguage),
                variant.Sku,
                cartItem.UnitPriceSnapshot,
                cartItem.Quantity,
                variant.RequiresInstallation,
                variant.WeightKg,
                variant.LengthCm,
                variant.WidthCm,
                variant.HeightCm,
                product.Images
                    .OrderBy(image => image.SortOrder)
                    .ThenBy(image => image.Id)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault()));
        }

        return Result<IReadOnlyCollection<CheckoutItemSnapshot>>.Success(snapshots);
    }

    public static ShippingParcel AggregateParcel(IReadOnlyCollection<CheckoutItemSnapshot> snapshots)
    {
        const decimal defaultWeightKg = 0.5m;
        const decimal defaultLengthCm = 15m;
        const decimal defaultWidthCm = 10m;
        const decimal defaultHeightCm = 8m;

        var totalWeight = 0m;
        var maxLength = 0m;
        var maxWidth = 0m;
        var totalHeight = 0m;

        foreach (var snapshot in snapshots)
        {
            var weight = snapshot.WeightKg ?? defaultWeightKg;
            var length = snapshot.LengthCm ?? defaultLengthCm;
            var width = snapshot.WidthCm ?? defaultWidthCm;
            var height = snapshot.HeightCm ?? defaultHeightCm;

            totalWeight += weight * snapshot.Quantity;
            if (length > maxLength) maxLength = length;
            if (width > maxWidth) maxWidth = width;
            totalHeight += height * snapshot.Quantity;
        }

        return new ShippingParcel
        {
            WeightKg = totalWeight,
            LengthCm = maxLength,
            WidthCm = maxWidth,
            HeightCm = totalHeight
        };
    }
}

internal sealed record CheckoutItemSnapshot(
    ProductVariant Variant,
    Guid ProductId,
    string ProductNameSnapshot,
    string SkuSnapshot,
    decimal UnitPrice,
    int Quantity,
    bool RequiresInstallation,
    decimal? WeightKg,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    string? ProductImageUrlSnapshot)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
