namespace WorkspaceEcommerce.Domain.Modules.Ordering;

public enum OrderStatus
{
    Pending = 0,
    Confirmed = 1,
    Processing = 2,
    Shipping = 3,
    Completed = 4,
    FailedDelivery = 5,
    Cancelled = 6
}
