using WorkspaceEcommerce.Domain.Common;

namespace WorkspaceEcommerce.Domain.Modules.Customers;

public sealed class CustomerLoginHistory : Entity
{
    public CustomerLoginHistory(
        Guid id,
        Guid customerId,
        string ipAddress,
        string userAgent,
        bool success)
        : base(id)
    {
        CustomerId = customerId;
        IpAddress = Guard.Required(ipAddress, nameof(IpAddress));
        UserAgent = Guard.Required(userAgent, nameof(UserAgent));
        Success = success;
        LoginTime = DateTimeOffset.UtcNow;
    }

    public Guid CustomerId { get; private set; }

    public DateTimeOffset LoginTime { get; private set; }

    public string IpAddress { get; private set; }

    public string UserAgent { get; private set; }

    public bool Success { get; private set; }
}
