using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class CustomerAddress : Entity
{
    public CustomerAddress(
        Guid id,
        Guid customerId,
        string label,
        string contactName,
        string contactPhone,
        string street,
        string ward,
        string province,
        bool isDefault)
        : base(id)
    {
        CustomerId = customerId;
        Label = Guard.Required(label, nameof(Label));
        ContactName = Guard.Required(contactName, nameof(ContactName));
        ContactPhone = Guard.Required(contactPhone, nameof(ContactPhone));
        Street = Guard.Required(street, nameof(Street));
        Ward = Guard.Required(ward, nameof(Ward));
        Province = Guard.Required(province, nameof(Province));
        IsDefault = isDefault;
    }

    public Guid CustomerId { get; private set; }

    public string Label { get; private set; }

    public string ContactName { get; private set; }

    public string ContactPhone { get; private set; }

    public string Street { get; private set; }

    public string Ward { get; private set; }

    public string Province { get; private set; }

    public bool IsDefault { get; private set; }

    public void Update(
        string label,
        string contactName,
        string contactPhone,
        string street,
        string ward,
        string province)
    {
        Label = Guard.Required(label, nameof(Label));
        ContactName = Guard.Required(contactName, nameof(ContactName));
        ContactPhone = Guard.Required(contactPhone, nameof(ContactPhone));
        Street = Guard.Required(street, nameof(Street));
        Ward = Guard.Required(ward, nameof(Ward));
        Province = Guard.Required(province, nameof(Province));
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}
