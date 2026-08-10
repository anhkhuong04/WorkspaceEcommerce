using System.Net;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.Auth;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AuthorizationMatrixIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task AnonymousCustomerAndAdminPrincipals_AreLimitedToTheirIntendedRoutes()
    {
        await fixture.ResetDatabaseAsync();
        using var anonymousClient = fixture.CreateClient();
        var adminToken = await anonymousClient.LoginAsAdminAsync();
        var customerToken = await anonymousClient.RegisterCustomerAsync();

        using var customerClient = fixture.CreateClient();
        customerClient.UseBearerToken(customerToken);
        using var adminClient = fixture.CreateClient();
        adminClient.UseBearerToken(adminToken);

        using var publicCatalogResponse = await anonymousClient.GetAsync("/api/products");
        using var anonymousCustomerResponse = await anonymousClient.GetAsync("/api/customer/orders");
        using var anonymousAdminResponse = await anonymousClient.GetAsync("/api/admin/orders");
        using var customerOrdersResponse = await customerClient.GetAsync("/api/customer/orders");
        using var customerAdminResponse = await customerClient.GetAsync("/api/admin/orders");
        using var adminOrdersResponse = await adminClient.GetAsync("/api/admin/orders");
        using var adminCustomerResponse = await adminClient.GetAsync("/api/customer/orders");

        Assert.Equal(HttpStatusCode.OK, publicCatalogResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousCustomerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAdminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, customerOrdersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerAdminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminOrdersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adminCustomerResponse.StatusCode);
    }
}
