using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Catalog;

namespace WorkspaceEcommerce.Application.Tests.Domain.Catalog;

public sealed class CategoryTests
{
    [Fact]
    public void MoveToParent_SameCategory_ThrowsDomainException()
    {
        var category = CreateCategory();

        var exception = Assert.Throws<DomainException>(() => category.MoveToParent(category.Id));

        Assert.Equal("Category cannot be its own parent.", exception.Message);
    }

    [Fact]
    public void MoveToParent_ValidParent_UpdatesParentId()
    {
        var category = CreateCategory();
        var parentId = Guid.NewGuid();

        category.MoveToParent(parentId);

        Assert.Equal(parentId, category.ParentId);
    }

    [Fact]
    public void Activate_InactiveCategory_SetsIsActiveTrue()
    {
        var category = CreateCategory(isActive: false);

        category.Activate();

        Assert.True(category.IsActive);
    }

    [Fact]
    public void Deactivate_ActiveCategory_SetsIsActiveFalse()
    {
        var category = CreateCategory(isActive: true);

        category.Deactivate();

        Assert.False(category.IsActive);
    }

    private static Category CreateCategory(bool isActive = true)
    {
        return new Category(Guid.NewGuid(), null, "Category", "category", 1, isActive);
    }
}
