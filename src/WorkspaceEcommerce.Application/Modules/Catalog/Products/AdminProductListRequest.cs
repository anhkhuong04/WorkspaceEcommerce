using FluentValidation;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Application.Modules.Catalog.Products;

public sealed record AdminProductListRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public Guid? ProductId { get; init; }
    public string? Ids { get; init; }
    public Guid? CategoryId { get; init; }
    public bool? IsActive { get; init; }
    public bool? IsFeatured { get; init; }
    public string? Stock { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string SortBy { get; init; } = "slug";
    public int NormalizedPageNumber => Math.Max(1, PageNumber);
    public int NormalizedPageSize => Math.Clamp(PageSize, 1, PaginationRequest.MaxPageSize);
    public int Skip => (NormalizedPageNumber - 1) * NormalizedPageSize;
}

public sealed class AdminProductListRequestValidator : AbstractValidator<AdminProductListRequest>
{
    public AdminProductListRequestValidator()
    {
        RuleFor(x => x.PageNumber).InclusiveBetween(1, 100000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(150);
        RuleFor(x => x.Ids).Must(value => value is null || value.Split(',') is { Length: > 0 and <= 100 } ids && ids.All(id => Guid.TryParse(id, out _)))
            .WithMessage("Choose at most 100 valid product IDs.");
        RuleFor(x => x.Stock).Must(x => x is null or "" or "in" or "out" or "low");
        RuleFor(x => x.SortBy).Must(x => x is "slug" or "created-desc" or "price-asc" or "price-desc" or "stock-asc");
        RuleFor(x => x.MinPrice).Must(WholeMoney).WithMessage("Minimum price must be a non-negative whole VND amount.");
        RuleFor(x => x.MaxPrice).Must(WholeMoney).WithMessage("Maximum price must be a non-negative whole VND amount.");
        RuleFor(x => x).Must(x => x.MinPrice is null || x.MaxPrice is null || x.MinPrice <= x.MaxPrice)
            .WithMessage("Maximum price must be greater than or equal to minimum price.");
    }
    private static bool WholeMoney(decimal? value) => value is null || value >= 0 && value == decimal.Truncate(value.Value);
}
