using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class StorefrontOrderLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_ExistingOrderCodeAndPhone_ReturnsOrderSnapshot()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder();
        dbContext.Seed(order);
        var service = CreateService(dbContext);

        var result = await service.LookupAsync(new OrderLookupRequest
        {
            OrderCode = " ord-20260608-abc12345 ",
            Phone = " 0900000000 "
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        var dto = result.Value.Order;
        Assert.Equal(order.Id, dto.Id);
        Assert.Equal("ORD-20260608-ABC12345", dto.OrderCode);
        Assert.Equal("0900000000", dto.CustomerPhone);
        Assert.Equal(OrderStatus.Pending, dto.Status);
        Assert.Equal(PaymentMethod.Cod, dto.PaymentMethod);
        Assert.Equal(200m, dto.TotalAmount);
        var item = Assert.Single(dto.Items);
        Assert.Equal("DESK-001", item.SkuSnapshot);
        Assert.Equal("Standing Desk", item.ProductNameSnapshot);
        Assert.Equal(100m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(200m, item.LineTotal);
    }

    [Fact]
    public async Task LookupAsync_OrderCodeDoesNotMatchPhone_ReturnsNotFound()
    {
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(CreateOrder());
        var service = CreateService(dbContext);

        var result = await service.LookupAsync(new OrderLookupRequest
        {
            OrderCode = "ORD-20260608-ABC12345",
            Phone = "0900000001"
        });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Contains("Order was not found.", result.Errors);
    }

    [Fact]
    public async Task LookupAsync_InvalidRequest_ReturnsValidation()
    {
        var service = CreateService(new FakeAppDbContext());

        var result = await service.LookupAsync(new OrderLookupRequest());

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains(result.Errors, error => error.Contains("Order Code", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("Phone", StringComparison.Ordinal));
    }

    private static StorefrontOrderLookupService CreateService(FakeAppDbContext dbContext)
    {
        return new StorefrontOrderLookupService(dbContext, new OrderLookupRequestValidator());
    }

    private static Order CreateOrder()
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-20260608-ABC12345",
            null,
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
            true);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }
}
