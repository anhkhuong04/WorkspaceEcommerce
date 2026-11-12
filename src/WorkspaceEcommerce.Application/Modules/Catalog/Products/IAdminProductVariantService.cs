using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public interface IAdminProductVariantService
{
    Task<Result<PagedResult<StockAdjustmentDto>>> GetStockHistoryAsync(Guid id, int pageNumber, CancellationToken cancellationToken = default);
    Task<Result<AdminProductVariantDto>> CreateVariantAsync(
        Guid productId,
        CreateProductVariantRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminProductVariantDto>> UpdateVariantAsync(
        Guid id,
        UpdateProductVariantRequest request,
        CancellationToken cancellationToken = default, string actor = "Admin");
}

public sealed record StockAdjustmentDto(Guid Id, int BeforeQuantity, int AfterQuantity, string Reason, string Actor, DateTimeOffset OccurredAt);
