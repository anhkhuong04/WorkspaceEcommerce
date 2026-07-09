using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Customers.Profile;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerProfileServiceTests
{
    [Fact]
    public async Task GetMeAsync_WhenCustomerIsAuthenticated_ReturnsCurrentCustomerProfile()
    {
        var customer = CreateCustomer();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);

        var result = await service.GetMeAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(customer.Id, result.Value!.Id);
        Assert.Equal("customer@example.com", result.Value.Email);
    }

    [Fact]
    public async Task GetMeAsync_WhenCustomerIsNotAuthenticated_ReturnsUnauthorized()
    {
        var service = CreateService(new FakeAppDbContext(), customerId: null);

        var result = await service.GetMeAsync();

        Assert.Equal(ResultStatus.Unauthorized, result.Status);
    }

    [Fact]
    public async Task GetMeAsync_WhenCustomerDoesNotExist_ReturnsNotFound()
    {
        var service = CreateService(new FakeAppDbContext(), Guid.NewGuid());

        var result = await service.GetMeAsync();

        Assert.Equal(ResultStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateMeAsync_WhenRequestIsValid_UpdatesCurrentCustomerProfile()
    {
        var customer = CreateCustomer();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);

        var result = await service.UpdateMeAsync(new UpdateCustomerProfileRequest(
            " Tran Thi B ",
            " 0911111111 "));

        Assert.True(result.IsSuccess);
        Assert.Equal("Tran Thi B", result.Value!.FullName);
        Assert.Equal("0911111111", result.Value.PhoneNumber);
        Assert.Equal("Tran Thi B", customer.FullName);
        Assert.Equal(1, dbContext.SaveChangesCallCount);
    }

    [Fact]
    public async Task UpdateMeAsync_WhenRequestIsInvalid_ReturnsValidation()
    {
        var customer = CreateCustomer();
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(customer);
        var service = CreateService(dbContext, customer.Id);

        var result = await service.UpdateMeAsync(new UpdateCustomerProfileRequest(
            string.Empty,
            string.Empty));

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Equal(0, dbContext.SaveChangesCallCount);
    }

    private static CustomerProfileService CreateService(FakeAppDbContext dbContext, Guid? customerId)
    {
        return new CustomerProfileService(
            dbContext,
            new StubCurrentCustomerContext(customerId),
            new UpdateCustomerProfileRequestValidator());
    }

    private static Customer CreateCustomer()
    {
        return Customer.Create(
            Guid.NewGuid(),
            "Nguyen Van A",
            "0900000000",
            "customer@example.com",
            "hash");
    }

    private sealed class StubCurrentCustomerContext(Guid? customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;

        public string? Email => "customer@example.com";
    }
}
