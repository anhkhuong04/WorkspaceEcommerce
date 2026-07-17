using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Abstractions.Persistence;

public interface IOrderReadStore
{
    IQueryable<Order> Orders { get; }

    IQueryable<OrderItem> OrderItems { get; }

    IQueryable<OrderStatusHistory> OrderStatusHistories { get; }
}
