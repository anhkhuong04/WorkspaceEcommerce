using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Customers;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Api.IntegrationTests.Customers;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class CustomerAccountIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task CustomerAuthProfileFlow_CompletesRegisterLoginMeAndUpdate()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var registerResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/register",
            new
            {
                fullName = "Nguyen Van A",
                phoneNumber = "0900000000",
                email = " CUSTOMER@EXAMPLE.COM ",
                password = "customer-password"
            });
        var registerJson = await registerResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.True(registerJson["success"]!.GetValue<bool>());
        Assert.Equal("customer@example.com", registerJson["data"]!["email"]!.GetValue<string>());
        var token = registerJson["data"]!["accessToken"]!.GetValue<string>();

        using var duplicateRegisterResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/register",
            new
            {
                fullName = "Nguyen Van B",
                phoneNumber = "0911111111",
                email = "customer@example.com",
                password = "customer-password"
            });

        Assert.Equal(HttpStatusCode.Conflict, duplicateRegisterResponse.StatusCode);

        using var unauthorizedMeResponse = await client.GetAsync("/api/customer/me");

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedMeResponse.StatusCode);

        client.UseBearerToken(token);

        using var meResponse = await client.GetAsync("/api/customer/me");
        var meJson = await meResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal("Nguyen Van A", meJson["data"]!["fullName"]!.GetValue<string>());

        using var updateResponse = await client.PutAsJsonAsync(
            "/api/customer/me",
            new
            {
                fullName = "Tran Thi B",
                phoneNumber = "0911111111"
            });
        var updateJson = await updateResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Tran Thi B", updateJson["data"]!["fullName"]!.GetValue<string>());
        Assert.Equal("0911111111", updateJson["data"]!["phoneNumber"]!.GetValue<string>());

        using var loginResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/login",
            new
            {
                email = "customer@example.com",
                password = "customer-password"
            });
        var loginJson = await loginResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(loginJson["data"]!["accessToken"]!.GetValue<string>()));

        using var invalidLoginResponse = await client.PostAsJsonAsync(
            "/api/customer/auth/login",
            new
            {
                email = "customer@example.com",
                password = "wrong-password"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, invalidLoginResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerOrders_ReturnOnlyAuthenticatedCustomerOrders()
    {
        await fixture.ResetDatabaseAsync();
        var catalog = TestData.CreateVisibleCatalog();
        using var client = fixture.CreateClient();
        var token = await client.RegisterCustomerAsync();
        client.UseBearerToken(token);
        var currentCustomerId = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.Customers
                .Where(customer => customer.Email == "customer@example.com")
                .Select(customer => customer.Id)
                .SingleAsync());
        var otherCustomer = Customer.Create(
            Guid.NewGuid(),
            "Tran Thi B",
            "0911111111",
            "other@example.com",
            "hash");
        var currentCustomerOrder = CreateOrder(
            Guid.NewGuid(),
            "ORD-CUSTOMER-001",
            currentCustomerId,
            catalog.Variant.Id,
            "Nguyen Van A",
            "0900000000",
            "customer@example.com");
        var otherCustomerOrder = CreateOrder(
            Guid.NewGuid(),
            "ORD-OTHER-001",
            otherCustomer.Id,
            catalog.Variant.Id,
            "Tran Thi B",
            "0911111111",
            "other@example.com");
        await fixture.SeedAsync(dbContext =>
        {
            dbContext.AddRange(
                catalog.Category,
                catalog.Product,
                catalog.Variant,
                otherCustomer,
                currentCustomerOrder,
                otherCustomerOrder);

            return Task.CompletedTask;
        });

        using var listResponse = await client.GetAsync("/api/customer/orders");
        var listJson = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var items = listJson["data"]!["items"]!.AsArray();
        var item = Assert.Single(items);
        Assert.Equal(currentCustomerOrder.Id, item!["id"]!.GetValue<Guid>());
        Assert.Equal("ORD-CUSTOMER-001", item["orderCode"]!.GetValue<string>());

        using var ownDetailResponse = await client.GetAsync($"/api/customer/orders/{currentCustomerOrder.Id}");
        var ownDetailJson = await ownDetailResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, ownDetailResponse.StatusCode);
        Assert.Equal(currentCustomerOrder.Id, ownDetailJson["data"]!["id"]!.GetValue<Guid>());
        Assert.Single(ownDetailJson["data"]!["items"]!.AsArray());
        Assert.Single(ownDetailJson["data"]!["statusHistory"]!.AsArray());

        using var otherDetailResponse = await client.GetAsync($"/api/customer/orders/{otherCustomerOrder.Id}");

        Assert.Equal(HttpStatusCode.NotFound, otherDetailResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerEndpoint_WithAdminToken_ReturnsForbidden()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        var adminToken = await client.LoginAsAdminAsync();
        client.UseBearerToken(adminToken);

        using var response = await client.GetAsync("/api/customer/me");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminEndpoint_WithCustomerToken_ReturnsForbidden()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        var customerToken = await client.RegisterCustomerAsync();
        client.UseBearerToken(customerToken);

        using var response = await client.GetAsync("/api/admin/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static Order CreateOrder(
        Guid id,
        string orderCode,
        Guid customerId,
        Guid productVariantId,
        string customerName,
        string customerPhone,
        string customerEmail)
    {
        var order = new Order(
            id,
            orderCode,
            customerId,
            customerName,
            customerPhone,
            customerEmail,
            "123 Shipping Street",
            "Call before delivery",
            PaymentMethod.Cod,
            "USD",
            1m);

        order.AddItem(
            Guid.NewGuid(),
            productVariantId,
            "Standing Desk",
            "DESK-001",
            100m,
            2,
            requiresInstallation: false);
        order.RecordCreated(Guid.NewGuid(), "Created by checkout.", changedBy: null);

        return order;
    }
}
