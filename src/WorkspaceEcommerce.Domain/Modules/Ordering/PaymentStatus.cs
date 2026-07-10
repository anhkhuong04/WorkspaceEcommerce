namespace WorkspaceEcommerce.Domain.Modules.Ordering;

public enum PaymentStatus
{
    Unpaid = 0,
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Cancelled = 4
}
