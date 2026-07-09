using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Orders;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerOrderServiceTests
{
    [Fact]
    public async Task GetOrdersAsync_ReturnsOnlyOrdersForCurrentCustomer()
    {
        var currentCustomerId = Guid.NewGuid();
        var otherCustomerId = Guid.NewGuid();
        var currentCustomerOrder = CreateOrder(Guid.NewGuid(), "ORD-CURRENT-001", currentCustomerId);
        var otherCustomerOrder = CreateOrder(Guid.NewGuid(), "ORD-OTHER-001", otherCustomerId);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(currentCustomerOrder, otherCustomerOrder);
        var service = CreateService(dbContext, currentCustomerId);

        var result = await service.GetOrdersAsync(new CustomerOrderListRequest());

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(currentCustomerOrder.Id, item.Id);
        Assert.Equal(1, item.ItemCount);
    }

    [Fact]
    public async Task GetOrdersAsync_WhenStatusFilterIsProvided_FiltersCurrentCustomerOrders()
    {
        var currentCustomerId = Guid.NewGuid();
        var pendingOrder = CreateOrder(Guid.NewGuid(), "ORD-CURRENT-001", currentCustomerId);
        var confirmedOrder = CreateOrder(Guid.NewGuid(), "ORD-CURRENT-002", currentCustomerId);
        confirmedOrder.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "admin@example.com");
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(pendingOrder, confirmedOrder);
        var service = CreateService(dbContext, currentCustomerId);

        var result = await service.GetOrdersAsync(new CustomerOrderListRequest
        {
            Status = OrderStatus.Confirmed
        });

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value!.Items);
        Assert.Equal(confirmedOrder.Id, item.Id);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WhenOrderBelongsToCurrentCustomer_ReturnsDetail()
    {
        var currentCustomerId = Guid.NewGuid();
        var order = CreateOrder(Guid.NewGuid(), "ORD-CURRENT-001", currentCustomerId);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(order);
        var service = CreateService(dbContext, currentCustomerId);

        var result = await service.GetOrderByIdAsync(order.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(order.Id, result.Value!.Id);
        Assert.Equal(currentCustomerId, result.Value.CustomerId);
        Assert.Single(result.Value.Items);
        Assert.Single(result.Value.StatusHistory);
    }

    [Fact]
    public async Task GetOrderByIdAsync_WhenOrderBelongsToAnotherCustomer_ReturnsNotFound()
    {
        var currentCustomerId = Guid.NewGuid();
        var order = CreateOrder(Guid.NewGuid(), "ORD-OTHER-001", Guid.NewGuid());
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(order);
        var service = CreateService(dbContext, currentCustomerId);

        var result = await service.GetOrderByIdAsync(order.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task GetOrdersAsync_WhenCustomerIsNotAuthenticated_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeAppDbContext(), customerId: null);

        var result = await service.GetOrdersAsync(new CustomerOrderListRequest());

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
    }

    private static CustomerOrderService CreateService(FakeAppDbContext dbContext, Guid? customerId)
    {
        return new CustomerOrderService(
            dbContext,
            new StubCurrentCustomerContext(customerId),
            new StubNotificationService(),
            new CustomerOrderListRequestValidator());
    }

    private static Order CreateOrder(Guid id, string orderCode, Guid customerId)
    {
        var order = new Order(
            id,
            orderCode,
            customerId,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod,
            "USD",
            1m);

        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Standing Desk",
            "DESK-001",
            100m,
            2,
            requiresInstallation: false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }

    private sealed class StubCurrentCustomerContext(Guid? customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;

        public string? Email => "customer@example.com";
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task NotifyCustomerAsync(Guid customerId, string eventType, object payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
