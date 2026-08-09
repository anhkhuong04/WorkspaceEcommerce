using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Categories;

internal sealed class AdminCategoryService(
    IAppDbContext dbContext,
    IValidator<CreateCategoryRequest> createValidator,
    IValidator<UpdateCategoryRequest> updateValidator) : IAdminCategoryService
{
    public async Task<Result<IReadOnlyCollection<AdminCategoryDto>>> GetCategoriesAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Categories
            .AsNoTrackingIfEf()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Slug)
            .ToArrayAsyncSafe(cancellationToken);

        var tree = BuildTree(categories);

        return Result<IReadOnlyCollection<AdminCategoryDto>>.Success(tree);
    }

    public async Task<Result<AdminCategoryDto>> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminCategoryDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (await SlugExistsAsync(normalizedSlug, cancellationToken: cancellationToken))
        {
            return Result<AdminCategoryDto>.Conflict("Category slug already exists.");
        }

        if (request.ParentId is not null && !await CategoryExistsAsync(request.ParentId.Value, cancellationToken))
        {
            return Result<AdminCategoryDto>.Validation(["Parent category does not exist."]);
        }

        var category = new Category(
            Guid.NewGuid(),
            request.ParentId,
            new LocalizedText(request.Name),
            normalizedSlug,
            request.SortOrder,
            request.IsActive);

        dbContext.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminCategoryDto>.Success(ToDto(category, []));
    }

    public async Task<Result<AdminCategoryDto>> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminCategoryDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var category = await dbContext.Categories
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (category is null)
        {
            return Result<AdminCategoryDto>.NotFound("Category was not found.");
        }

        var normalizedSlug = NormalizeSlug(request.Slug);
        if (await SlugExistsAsync(normalizedSlug, id, cancellationToken))
        {
            return Result<AdminCategoryDto>.Conflict("Category slug already exists.");
        }

        if (request.ParentId == id)
        {
            return Result<AdminCategoryDto>.Validation(["Category cannot be its own parent."]);
        }

        if (request.ParentId is not null)
        {
            if (!await CategoryExistsAsync(request.ParentId.Value, cancellationToken))
            {
                return Result<AdminCategoryDto>.Validation(["Parent category does not exist."]);
            }

            if (await WouldCreateCycleAsync(id, request.ParentId.Value, cancellationToken))
            {
                return Result<AdminCategoryDto>.Validation(["Category parent would create a cycle."]);
            }
        }

        category.UpdateDetails(new LocalizedText(request.Name), normalizedSlug, request.SortOrder);
        category.MoveToParent(request.ParentId);

        if (request.IsActive)
        {
            category.Activate();
        }
        else
        {
            category.Deactivate();
        }

        dbContext.Update(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminCategoryDto>.Success(ToDto(category, []));
    }

    public async Task<Result<AdminCategoryDto>> DeleteCategoryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (category is null)
        {
            return Result<AdminCategoryDto>.NotFound("Category was not found.");
        }

        if (await dbContext.Categories
                .Where(existing => existing.ParentId == id)
                .AnyAsyncSafe(cancellationToken))
        {
            return Result<AdminCategoryDto>.Conflict("Category has child categories and cannot be deleted.");
        }

        if (await dbContext.Products
                .Where(product => product.CategoryId == id)
                .AnyAsyncSafe(cancellationToken))
        {
            return Result<AdminCategoryDto>.Conflict("Category has products and cannot be deleted. Move or delete them first.");
        }

        var dto = ToDto(category, []);
        dbContext.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminCategoryDto>.Success(dto);
    }

    private static IReadOnlyCollection<AdminCategoryDto> BuildTree(IReadOnlyCollection<Category> categories)
    {
        return categories
            .Where(category => category.ParentId is null)
            .Select(category => ToDto(category, BuildChildren(category.Id, categories)))
            .ToArray();
    }

    private static IReadOnlyCollection<AdminCategoryDto> BuildChildren(
        Guid parentId,
        IReadOnlyCollection<Category> categories)
    {
        return categories
            .Where(category => category.ParentId == parentId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Slug)
            .Select(category => ToDto(category, BuildChildren(category.Id, categories)))
            .ToArray();
    }

    private static AdminCategoryDto ToDto(Category category, IReadOnlyCollection<AdminCategoryDto> children)
    {
        return new AdminCategoryDto(
            category.Id,
            category.ParentId,
            category.Name,
            category.Slug,
            category.IsActive,
            category.SortOrder,
            children);
    }

    private Task<bool> SlugExistsAsync(
        string slug,
        Guid? excludedCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Categories
            .Where(category => category.Slug == slug &&
                (excludedCategoryId == null || category.Id != excludedCategoryId.Value))
            .AnyAsyncSafe(cancellationToken);
    }

    private Task<bool> CategoryExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .Where(category => category.Id == id)
            .AnyAsyncSafe(cancellationToken);
    }

    private async Task<bool> WouldCreateCycleAsync(
        Guid categoryId,
        Guid parentId,
        CancellationToken cancellationToken)
    {
        var categoriesById = await dbContext.Categories
            .AsNoTrackingIfEf()
            .ToDictionaryAsyncSafe(category => category.Id, cancellationToken);
        var currentParentId = parentId;

        while (categoriesById.TryGetValue(currentParentId, out var parent))
        {
            if (parent.Id == categoryId)
            {
                return true;
            }

            if (parent.ParentId is null)
            {
                return false;
            }

            currentParentId = parent.ParentId.Value;
        }

        return false;
    }

    private static string NormalizeSlug(string slug)
    {
        return slug.Trim().ToLowerInvariant();
    }
}
