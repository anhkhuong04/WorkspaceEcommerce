using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class AdminOrderServiceTests
{
    [Fact]
    public async Task GetOrdersAsync_ReturnsPagedOrdersWithItemCount()
    {
        var dbContext = new FakeAppDbContext();
        var firstOrder = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        firstOrder.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 100m, 1, false);
        var secondOrder = CreateOrder("ORD-20260608-0002", "0900000002", "Tran Van B");
        secondOrder.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Chair", "CHAIR-001", 50m, 2, false);
        dbContext.Seed(firstOrder, secondOrder);
        var service = CreateService(dbContext);

        var result = await service.GetOrdersAsync(new AdminOrderListRequest
        {
            Search = "0002",
            PageNumber = 1,
            PageSize = 10
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.TotalCount);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(secondOrder.Id, item.Id);
        Assert.Equal("ORD-20260608-0002", item.OrderCode);
        Assert.Equal(1, item.ItemCount);
        Assert.Equal(100m, item.TotalAmount);
    }

    [Fact]
    public async Task GetOrdersAsync_InvalidStatusFilter_ReturnsValidation()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.GetOrdersAsync(new AdminOrderListRequest
        {
            Status = (OrderStatus)999
        });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("Status", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetOrderByIdAsync_ExistingOrder_ReturnsItemsAndStatusHistory()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        order.AddItem(Guid.NewGuid(), Guid.NewGuid(), "Standing Desk", "DESK-001", 100m, 1, false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.GetOrderByIdAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(order.Id, result.Value.Id);
        Assert.Single(result.Value.Items);
        var history = Assert.Single(result.Value.StatusHistory);
        Assert.Null(history.FromStatus);
        Assert.Equal(OrderStatus.Pending, history.ToStatus);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidTransition_UpdatesStatusAndRecordsHistory()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest
            {
                Status = OrderStatus.Confirmed,
                Note = "Confirmed by admin"
            },
            "admin@example.com");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(OrderStatus.Confirmed, result.Value.Status);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
        Assert.Equal(2, result.Value.StatusHistory.Count);
        var latestHistory = result.Value.StatusHistory.Last();
        Assert.Equal(OrderStatus.Pending, latestHistory.FromStatus);
        Assert.Equal(OrderStatus.Confirmed, latestHistory.ToStatus);
        Assert.Equal("Confirmed by admin", latestHistory.Note);
        Assert.Equal("admin@example.com", latestHistory.ChangedBy);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ReturnsConflict()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest { Status = OrderStatus.Completed },
            "admin@example.com");

        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Contains("Order status cannot change from Pending to Completed.", result.Errors);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateStatusAsync_MissingOrder_ReturnsNotFound()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.UpdateStatusAsync(
            Guid.NewGuid(),
            new UpdateOrderStatusRequest { Status = OrderStatus.Confirmed },
            "admin@example.com");

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Order was not found.", result.Errors);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidRequest_ReturnsValidation()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder("ORD-20260608-0001", "0900000001", "Nguyen Van A");
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.UpdateStatusAsync(
            order.Id,
            new UpdateOrderStatusRequest { Status = (OrderStatus)999 },
            "admin@example.com");

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("Status", StringComparison.Ordinal));
    }

    private static AdminOrderService CreateService(FakeAppDbContext dbContext)
    {
        return new AdminOrderService(
            dbContext,
            new AdminOrderListRequestValidator(),
            new UpdateOrderStatusRequestValidator());
    }

    private static Order CreateOrder(string orderCode, string phone, string customerName)
    {
        return new Order(
            Guid.NewGuid(),
            orderCode,
            null,
            customerName,
            phone,
            "customer@example.com",
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod,
            "USD",
            1m);
    }
}
