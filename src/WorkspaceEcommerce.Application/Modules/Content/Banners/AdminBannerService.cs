using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Domain.Modules.Content;

namespace WorkspaceEcommerce.Application.Modules.Content.Banners;

internal sealed class AdminBannerService(
    IAppDbContext dbContext,
    IValidator<CreateBannerRequest> createValidator,
    IValidator<UpdateBannerRequest> updateValidator) : IAdminBannerService
{
    public Task<Result<IReadOnlyCollection<AdminBannerDto>>> GetBannersAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var banners = dbContext.Banners
            .OrderBy(banner => banner.SortOrder)
            .ThenBy(banner => banner.Title)
            .Select(ToDto)
            .ToArray();

        return Task.FromResult(Result<IReadOnlyCollection<AdminBannerDto>>.Success(banners));
    }

    public async Task<Result<AdminBannerDto>> CreateBannerAsync(
        CreateBannerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminBannerDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var banner = new Banner(
            Guid.NewGuid(),
            request.Title,
            request.ImageUrl,
            request.LinkUrl,
            request.SortOrder,
            request.IsActive);

        dbContext.Add(banner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminBannerDto>.Success(ToDto(banner));
    }

    public async Task<Result<AdminBannerDto>> UpdateBannerAsync(
        Guid id,
        UpdateBannerRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result<AdminBannerDto>.Validation(validationResult.Errors.Select(error => error.ErrorMessage));
        }

        var banner = dbContext.Banners.FirstOrDefault(existing => existing.Id == id);
        if (banner is null)
        {
            return Result<AdminBannerDto>.NotFound("Banner was not found.");
        }

        banner.UpdateDetails(request.Title, request.ImageUrl, request.LinkUrl, request.SortOrder);
        if (request.IsActive)
        {
            banner.Activate();
        }
        else
        {
            banner.Deactivate();
        }

        dbContext.Update(banner);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AdminBannerDto>.Success(ToDto(banner));
    }

    private static AdminBannerDto ToDto(Banner banner)
    {
        return new AdminBannerDto(
            banner.Id,
            banner.Title,
            banner.ImageUrl,
            banner.LinkUrl,
            banner.SortOrder,
            banner.IsActive,
            banner.CreatedAt,
            banner.UpdatedAt);
    }
}
