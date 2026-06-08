using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; init; }

    public string? Note { get; init; }
}
