using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public interface IShippingRateAdminService
{
    ShippingCatalogSummaryDto GetCatalog();
    Result<PagedResult<ShippingWard>> GetWards(string provinceCode, int pageNumber, int pageSize);
    Task<Result<PagedResult<ShippingWeightIssueDto>>> WeightIssuesAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);
    Task<Result<PagedResult<ShippingRatePlanSummaryDto>>> ListAsync(int pageNumber, int pageSize,
        ShippingRatePlanStatus? status, CancellationToken cancellationToken);
    Task<Result<ShippingRatePlanDto>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ShippingRatePlanDto>> CreateAsync(SaveShippingRatePlanRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<ShippingRatePlanDto>> UpdateAsync(Guid id, SaveShippingRatePlanRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<ShippingRatePlanDto>> CloneAsync(Guid id, CloneShippingRatePlanRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<ShippingRatePlanDto>> PublishAsync(Guid id, PublishShippingRatePlanRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<ShippingRatePlanDto>> RetireAsync(Guid id, ShippingRateActionRequest request, string actor, CancellationToken cancellationToken);
    Task<Result<ShippingRateCoverageResult>> ValidateAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<ShippingRatePreviewDto>> PreviewAsync(Guid id, ShippingRatePreviewRequest request, CancellationToken cancellationToken);
    Task<Result<PagedResult<ShippingRateAuditDto>>> HistoryAsync(Guid id, int pageNumber, int pageSize, CancellationToken cancellationToken);
}
