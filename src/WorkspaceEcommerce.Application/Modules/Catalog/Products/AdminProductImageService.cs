using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Common.Persistence;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

internal sealed class AdminProductImageService(
    ICatalogReadStore catalogStore,
    IAppWriteStore writeStore,
    IValidator<CreateProductImageRequest> createValidator,
    IValidator<UpdateProductImageRequest> updateValidator) : IAdminProductImageService
{
    public async Task<Result<AdminProductImageDto>> CreateImageAsync(
        Guid productId,
        CreateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductImageDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var product = await catalogStore.Products
            .Where(existing => existing.Id == productId)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (product is null)
        {
            return Result<AdminProductImageDto>.NotFound("Product was not found.");
        }

        try
        {
            var image = product.AddImage(
                Guid.NewGuid(),
                request.ImageUrl,
                request.AltText,
                request.SortOrder);

            writeStore.Update(product);
            writeStore.Add(image);
            await writeStore.SaveChangesAsync(cancellationToken);

            return Result<AdminProductImageDto>.Success(ToDto(image));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductImageDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductImageDto>> UpdateImageAsync(
        Guid id,
        UpdateProductImageRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminProductImageDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var image = await catalogStore.ProductImages
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (image is null)
        {
            return Result<AdminProductImageDto>.NotFound("Product image was not found.");
        }

        try
        {
            image.Update(request.ImageUrl, request.AltText, request.SortOrder);

            writeStore.Update(image);
            await writeStore.SaveChangesAsync(cancellationToken);

            return Result<AdminProductImageDto>.Success(ToDto(image));
        }
        catch (DomainException exception)
        {
            return Result<AdminProductImageDto>.Validation([exception.Message]);
        }
    }

    public async Task<Result<AdminProductImageDto>> DeleteImageAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var image = await catalogStore.ProductImages
            .Where(existing => existing.Id == id)
            .FirstOrDefaultAsyncSafe(cancellationToken);
        if (image is null)
        {
            return Result<AdminProductImageDto>.NotFound("Product image was not found.");
        }

        var dto = ToDto(image);
        writeStore.Remove(image);
        await writeStore.SaveChangesAsync(cancellationToken);

        return Result<AdminProductImageDto>.Success(dto);
    }

    private static AdminProductImageDto ToDto(ProductImage image)
    {
        return new AdminProductImageDto(
            image.Id,
            image.ProductId,
            image.ImageUrl,
            image.AltText,
            image.SortOrder);
    }
}
