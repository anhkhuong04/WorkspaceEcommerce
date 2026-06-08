using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Content.Banners;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Content;

namespace WorkspaceEcommerce.Application.Tests.Modules.Content.Banners;

public sealed class AdminBannerServiceTests
{
    [Fact]
    public async Task CreateBannerAsync_ValidRequest_CreatesBanner()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateBannerAsync(new CreateBannerRequest
        {
            Title = "Hero Desk Setup",
            ImageUrl = "https://example.test/banner.jpg",
            LinkUrl = "https://example.test/desks",
            SortOrder = 2,
            IsActive = true
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Hero Desk Setup", result.Value.Title);
        Assert.Equal("https://example.test/banner.jpg", result.Value.ImageUrl);
        Assert.Single(dbContext.Banners);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateBannerAsync_InvalidRequest_ReturnsValidation()
    {
        var dbContext = new FakeAppDbContext();
        var service = CreateService(dbContext);

        var result = await service.CreateBannerAsync(new CreateBannerRequest
        {
            Title = string.Empty,
            ImageUrl = string.Empty
        });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBannerAsync_ExistingBanner_UpdatesAndDeactivatesBanner()
    {
        var banner = CreateBanner(isActive: true);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(banner);
        var service = CreateService(dbContext);

        var result = await service.UpdateBannerAsync(banner.Id, new UpdateBannerRequest
        {
            Title = "Updated Banner",
            ImageUrl = "https://example.test/updated.jpg",
            LinkUrl = null,
            SortOrder = 9,
            IsActive = false
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Banner", result.Value.Title);
        Assert.False(result.Value.IsActive);
        Assert.False(banner.IsActive);
        Assert.Equal(9, banner.SortOrder);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateBannerAsync_MissingBanner_ReturnsNotFound()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.UpdateBannerAsync(Guid.NewGuid(), new UpdateBannerRequest
        {
            Title = "Missing",
            ImageUrl = "https://example.test/missing.jpg"
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Banner was not found.", result.Errors);
    }

    [Fact]
    public async Task GetBannersAsync_ExistingBanners_ReturnsSortOrderThenTitle()
    {
        var first = CreateBanner(title: "B", sortOrder: 2);
        var second = CreateBanner(title: "A", sortOrder: 1);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(first, second);
        var service = CreateService(dbContext);

        var result = await service.GetBannersAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Collection(
            result.Value,
            banner => Assert.Equal(second.Id, banner.Id),
            banner => Assert.Equal(first.Id, banner.Id));
    }

    private static AdminBannerService CreateService(FakeAppDbContext dbContext)
    {
        return new AdminBannerService(
            dbContext,
            new CreateBannerRequestValidator(),
            new UpdateBannerRequestValidator());
    }

    private static Banner CreateBanner(
        string title = "Banner",
        int sortOrder = 1,
        bool isActive = true)
    {
        return new Banner(
            Guid.NewGuid(),
            title,
            "https://example.test/banner.jpg",
            "https://example.test",
            sortOrder,
            isActive);
    }
}
