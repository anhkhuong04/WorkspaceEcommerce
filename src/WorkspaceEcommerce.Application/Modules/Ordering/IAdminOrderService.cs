using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public interface IAdminOrderService
{
    Task<Result<PagedResult<AdminOrderListItemDto>>> GetOrdersAsync(
        AdminOrderListRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminOrderDto>> GetOrderByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<AdminOrderDto>> UpdateStatusAsync(
        Guid id,
        UpdateOrderStatusRequest request,
        string? changedBy,
        CancellationToken cancellationToken = default);

    Task<Result<AdminOrderImportPreviewDto>> PreviewImportAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<Result<AdminOrderImportCommitResultDto>> CommitImportAsync(
        Stream content,
        string fileName,
        string? changedBy,
        CancellationToken cancellationToken = default);
}
