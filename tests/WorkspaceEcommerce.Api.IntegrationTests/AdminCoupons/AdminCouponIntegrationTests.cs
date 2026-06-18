using System.Net;
using System.Net.Http.Json;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

namespace WorkspaceEcommerce.Api.IntegrationTests.AdminCoupons;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class AdminCouponIntegrationTests(ApiIntegrationTestFixture fixture)
{
    [Fact]
    public async Task AdminCouponEndpoints_WithBearerToken_CreateListGetUpdateStatusAndDelete()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var createResponse = await client.PostAsJsonAsync(
            "/api/admin/coupons",
            new
            {
                code = "summer10",
                name = "Summer 10",
                description = "Ten percent off",
                discountType = 0,
                discountValue = 10m,
                maxDiscountAmount = 50m,
                minimumSubtotal = 100m,
                startsAt = DateTimeOffset.UtcNow.AddDays(-1),
                endsAt = DateTimeOffset.UtcNow.AddDays(7),
                usageLimit = 100,
                isActive = true,
                productTargetIds = Array.Empty<Guid>()
            });
        var createJson = await createResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(createJson["success"]!.GetValue<bool>());
        Assert.Equal("SUMMER10", createJson["data"]!["code"]!.GetValue<string>());
        var couponId = createJson["data"]!["id"]!.GetValue<Guid>();

        using var listResponse = await client.GetAsync("/api/admin/coupons?search=summer&isActive=true");
        var listJson = await listResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listJson["data"]!["totalCount"]!.GetValue<int>());
        Assert.Single(listJson["data"]!["items"]!.AsArray());

        using var detailResponse = await client.GetAsync($"/api/admin/coupons/{couponId}");
        var detailJson = await detailResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.Equal(couponId, detailJson["data"]!["id"]!.GetValue<Guid>());

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/admin/coupons/{couponId}",
            new
            {
                code = "summer20",
                name = "Summer 20",
                description = (string?)null,
                discountType = 1,
                discountValue = 20m,
                maxDiscountAmount = (decimal?)null,
                minimumSubtotal = (decimal?)null,
                startsAt = (DateTimeOffset?)null,
                endsAt = (DateTimeOffset?)null,
                usageLimit = (int?)null,
                isActive = false,
                productTargetIds = Array.Empty<Guid>()
            });
        var updateJson = await updateResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("SUMMER20", updateJson["data"]!["code"]!.GetValue<string>());
        Assert.False(updateJson["data"]!["isActive"]!.GetValue<bool>());

        using var statusResponse = await client.PatchAsJsonAsync(
            $"/api/admin/coupons/{couponId}/status",
            new { isActive = true });
        var statusJson = await statusResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.True(statusJson["data"]!["isActive"]!.GetValue<bool>());

        using var deleteResponse = await client.DeleteAsync($"/api/admin/coupons/{couponId}");
        using var emptyListResponse = await client.GetAsync("/api/admin/coupons");
        var emptyListJson = await emptyListResponse.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Equal(0, emptyListJson["data"]!["totalCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task AdminCouponEndpoints_WithoutBearerToken_ReturnUnauthorized()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();

        using var response = await client.GetAsync("/api/admin/coupons");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateCoupon_DuplicateCode_ReturnsConflictEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());
        await CreateCouponAsync(client, "WELCOME10");

        using var response = await client.PostAsJsonAsync(
            "/api/admin/coupons",
            new
            {
                code = "welcome10",
                name = "Welcome duplicate",
                discountType = 0,
                discountValue = 10m,
                isActive = true,
                productTargetIds = Array.Empty<Guid>()
            });
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains("Coupon code already exists.", json["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    [Fact]
    public async Task CreateCoupon_InvalidProductTarget_ReturnsValidationEnvelope()
    {
        await fixture.ResetDatabaseAsync();
        using var client = fixture.CreateClient();
        client.UseBearerToken(await client.LoginAsAdminAsync());

        using var response = await client.PostAsJsonAsync(
            "/api/admin/coupons",
            new
            {
                code = "TARGET10",
                name = "Target 10",
                discountType = 0,
                discountValue = 10m,
                isActive = true,
                productTargetIds = new[] { Guid.NewGuid() }
            });
        var json = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(json["success"]!.GetValue<bool>());
        Assert.Contains("Coupon target product does not exist.", json["errors"]!.AsArray().Select(error => error!.GetValue<string>()));
    }

    private static async Task CreateCouponAsync(HttpClient client, string code)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/admin/coupons",
            new
            {
                code,
                name = code,
                discountType = 0,
                discountValue = 10m,
                isActive = true,
                productTargetIds = Array.Empty<Guid>()
            });

        response.EnsureSuccessStatusCode();
    }
}
