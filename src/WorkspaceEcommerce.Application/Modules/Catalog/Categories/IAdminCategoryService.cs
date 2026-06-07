using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Categories;

public interface IAdminCategoryService
{
    Task<Result<IReadOnlyCollection<AdminCategoryDto>>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<Result<AdminCategoryDto>> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<AdminCategoryDto>> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default);
}
