using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Warranties;

public interface IAdminWarrantyService
{
    Task<Result<PagedResult<AdminWarrantyPlanDto>>> GetPlansAsync(AdminWarrantyPlanListRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyPlanDto>> CreatePlanAsync(CreateWarrantyPlanRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyPlanDto>> RetirePlanAsync(Guid id, string actorId, CancellationToken cancellationToken = default);
    Task<Result> AssignPlanToVariantAsync(Guid variantId, AssignWarrantyPlanRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyImportResultDto>> ImportUnitsAsync(ImportWarrantyUnitsRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<AdminWarrantyUnitDto>>> GetUnitsAsync(AdminWarrantyUnitListRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyEntitlementDto>> AssignUnitAsync(Guid unitId, AssignWarrantyUnitRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<AdminWarrantyEntitlementDto>>> GetEntitlementsAsync(AdminWarrantyEntitlementListRequest request, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyEntitlementDto>> GetEntitlementAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyEntitlementDto>> ActivateAsync(Guid id, string actorId, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyEntitlementDto>> VoidAsync(Guid id, AdminWarrantyReasonRequest request, string actorId, CancellationToken cancellationToken = default);
    Task<Result<AdminWarrantyEntitlementDto>> ReplaceAsync(Guid id, ReplaceWarrantyRequest request, string actorId, CancellationToken cancellationToken = default);
}
