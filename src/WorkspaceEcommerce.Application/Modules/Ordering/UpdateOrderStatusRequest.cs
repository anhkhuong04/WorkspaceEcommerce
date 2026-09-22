using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class UpdateOrderStatusRequest
{
    public OrderStatus Status { get; init; }

    public string? InternalNote { get; init; }

    public string? CancellationReason { get; init; }

    public string? CustomerMessage { get; init; }
}
