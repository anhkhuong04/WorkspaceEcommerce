using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Modules.Admin.Authentication;
using WorkspaceEcommerce.Application.Modules.Customers.Authentication;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Customers;

namespace WorkspaceEcommerce.Application.Tests.Modules.Customers;

public sealed class CustomerSessionServiceTests
{
    [Fact]
    public async Task RefreshAsync_RotatesHashedTokenAndReplayRevokesTheEntireFamily()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext);

        var issued = await service.IssueAsync(customer);
        var originalRefreshToken = issued.RefreshToken!;
        var rotated = await service.RefreshAsync(originalRefreshToken);
        var replayed = await service.RefreshAsync(originalRefreshToken);

        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(originalRefreshToken, rotated.Value!.RefreshToken);
        Assert.Single(dbContext.CustomerRefreshTokenFamilies);
        Assert.Equal(2, dbContext.CustomerRefreshTokens.Count());
        Assert.All(dbContext.CustomerRefreshTokens, token => Assert.NotEqual(originalRefreshToken, token.TokenHash));
        Assert.Equal(WorkspaceEcommerce.Application.Common.Models.ResultStatus.Unauthorized, replayed.Status);
        Assert.Equal("refresh_token_reuse", dbContext.CustomerRefreshTokenFamilies.Single().RevocationReason);

        var replacementAfterReplay = await service.RefreshAsync(rotated.Value.RefreshToken!);
        Assert.Equal(WorkspaceEcommerce.Application.Common.Models.ResultStatus.Unauthorized, replacementAfterReplay.Status);
    }

    [Fact]
    public async Task RevokeAsync_LogoutPreventsSubsequentRefresh()
    {
        var dbContext = new FakeAppDbContext();
        var customer = CreateCustomer();
        dbContext.Seed(customer);
        var service = CreateService(dbContext);
        var issued = await service.IssueAsync(customer);

        await service.RevokeAsync(issued.RefreshToken!, "logout");
        var refreshed = await service.RefreshAsync(issued.RefreshToken!);

        Assert.Equal(WorkspaceEcommerce.Application.Common.Models.ResultStatus.Unauthorized, refreshed.Status);
        Assert.Equal("logout", dbContext.CustomerRefreshTokenFamilies.Single().RevocationReason);
    }

    private static CustomerSessionService CreateService(FakeAppDbContext dbContext) => new(
        dbContext,
        new StubTokenGenerator(),
        new CustomerAccountLifecycleOptions
        {
            RefreshTokenLifetimeDays = 30,
            StorefrontBaseUrl = "https://storefront.example.test"
        },
        TimeProvider.System);

    private static Customer CreateCustomer() => Customer.Create(
        Guid.NewGuid(),
        "Nguyen Van A",
        "0900000000",
        "customer@example.com",
        "password-hash");

    private sealed class StubTokenGenerator : IJwtTokenGenerator
    {
        public AdminLoginResponse GenerateAdminToken(string email) => throw new NotSupportedException();

        public CustomerAuthResponse GenerateCustomerToken(Guid customerId, string email, string fullName, string? phoneNumber) =>
            new("access-token", "Bearer", DateTimeOffset.UtcNow.AddMinutes(15), customerId, email, fullName, phoneNumber ?? string.Empty);
    }
}
