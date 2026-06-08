using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using WorkspaceEcommerce.Domain.Modules.Content;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Infrastructure.Tests.Persistence;

public sealed class ContentModelConfigurationTests
{
    [Fact]
    public void Banner_IsMappedToContentSchema()
    {
        var metadata = GetEntityType(typeof(Banner));

        Assert.Equal("content", metadata.GetSchema());
        Assert.Equal("banners", metadata.GetTableName());
    }

    [Theory]
    [InlineData(nameof(Banner.IsActive), "ix_banners_is_active")]
    [InlineData(nameof(Banner.SortOrder), "ix_banners_sort_order")]
    public void BannerLookupFields_HaveIndexes(string propertyName, string databaseName)
    {
        var metadata = GetEntityType(typeof(Banner));
        var index = metadata.GetIndexes().Single(candidate =>
            candidate.Properties.Count == 1
            && candidate.Properties[0].Name == propertyName);

        Assert.Equal(databaseName, index.GetDatabaseName());
    }

    [Theory]
    [InlineData(nameof(Banner.Title), 250)]
    [InlineData(nameof(Banner.ImageUrl), 1000)]
    [InlineData(nameof(Banner.LinkUrl), 1000)]
    public void BannerStringFields_HaveExpectedMaxLength(string propertyName, int maxLength)
    {
        var property = GetEntityType(typeof(Banner)).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static IReadOnlyEntityType GetEntityType(Type clrType)
    {
        var entityType = CreateModel().FindEntityType(clrType);

        Assert.NotNull(entityType);
        return entityType;
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=metadata_only;Username=test;Password=test")
            .Options;

        using var dbContext = new AppDbContext(options);

        return dbContext.Model;
    }
}
