using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

internal sealed class AdminProductSpecificationService(
    ICatalogReadStore catalogStore,
    IAppWriteStore writeStore,
    IValidator<CreateProductSpecificationRequest> createValidator,
    IValidator<UpdateProductSpecificationRequest> updateValidator) : IAdminProductSpecificationService
{
    public async Task<Result<AdminProductSpecificationDto>> CreateSpecificationAsync(
        Guid productId,
        CreateProductSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductSpecificationDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = await catalogStore.Products
            .Where(existing => existing.Id == productId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (product is null)
        {
            return Result<AdminProductSpecificationDto>.NotFound("Product was not found.");
        }

        try
        {
            var specification = product.AddSpecification(
                Guid.NewGuid(),
                request.Name,
                request.Value,
                request.SortOrder);

            writeStore.Update(product);
            writeStore.Add(specification);
            await writeStore.SaveChangesAsync(cancellationToken);

            return Result<AdminProductSpecificationDto>.Success(ToDto(specification));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductSpecificationDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductSpecificationDto>> UpdateSpecificationAsync(
        Guid id,
        UpdateProductSpecificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductSpecificationDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var specification = await catalogStore.ProductSpecifications
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (specification is null)
        {
            return Result<AdminProductSpecificationDto>.NotFound("Product specification was not found.");
        }

        try
        {
            specification.Update(request.Name, request.Value, request.SortOrder);

            writeStore.Update(specification);
            await writeStore.SaveChangesAsync(cancellationToken);

            return Result<AdminProductSpecificationDto>.Success(ToDto(specification));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductSpecificationDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductSpecificationDto>> DeleteSpecificationAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var specification = await catalogStore.ProductSpecifications
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (specification is null)
        {
            return Result<AdminProductSpecificationDto>.NotFound("Product specification was not found.");
        }

        var dto = ToDto(specification);
        writeStore.Remove(specification);
        await writeStore.SaveChangesAsync(cancellationToken);

        return Result<AdminProductSpecificationDto>.Success(dto);
    }

    private static AdminProductSpecificationDto ToDto(ProductSpecification specification)
    {
        return new AdminProductSpecificationDto(
            specification.Id,
            specification.ProductId,
            specification.Name,
            specification.Value,
            specification.SortOrder);
    }
}
