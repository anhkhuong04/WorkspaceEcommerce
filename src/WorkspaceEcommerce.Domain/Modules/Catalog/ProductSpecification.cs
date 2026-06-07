using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Catalog;

public sealed class ProductSpecification : Entity
{
    public ProductSpecification(
        Guid id,
        Guid productId,
        string name,
        string value,
        int sortOrder)
        : base(id)
    {
        if (productId == Guid.Empty)
        {
            throw new DomainException("Product specification product id cannot be empty.");
        }

        ProductId = productId;
        Name = Guard.Required(name, nameof(Name));
        Value = Guard.Required(value, nameof(Value));
        SortOrder = sortOrder;
    }

    public Guid ProductId { get; private set; }

    public string Name { get; private set; }

    public string Value { get; private set; }

    public int SortOrder { get; private set; }

    public void Update(string name, string value, int sortOrder)
    {
        Name = Guard.Required(name, nameof(Name));
        Value = Guard.Required(value, nameof(Value));
        SortOrder = sortOrder;
    }
}
