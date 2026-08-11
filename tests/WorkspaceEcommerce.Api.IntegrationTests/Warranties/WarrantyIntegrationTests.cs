using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Api.IntegrationTests.Warranties;

[Collection(ApiIntegrationTestCollection.Name)]
public sealed class WarrantyIntegrationTests(ApiIntegrationTestFixture fixture)
{
    private const string Identifier = "SERIAL-001";
    private const string IdentifierKey = "integration-warranty-hmac-key-at-least-32-characters";

    [Fact]
    public async Task OwnerCanActivate_AndPublicLookupIsPrivacySafeAndNotCached()
    {
        await fixture.ResetDatabaseAsync();
        using var customerClient = fixture.CreateClient();
        var customerToken = await customerClient.RegisterCustomerAsync();
        var customerId = await fixture.ExecuteDbAsync(dbContext => dbContext.Customers
            .Where(customer => customer.Email == "customer@example.com")
            .Select(customer => customer.Id)
            .SingleAsync());
        await SeedPendingWarrantyAsync(customerId);

        using var publicClient = fixture.CreateClient();
        using var beforeActivation = await publicClient.PostAsJsonAsync("/api/warranties/lookup", new { identifier = Identifier });
        var beforeJson = await beforeActivation.ReadJsonAsync();
        var beforeBody = beforeJson.ToJsonString();

        Assert.Equal(HttpStatusCode.OK, beforeActivation.StatusCode);
        Assert.Contains("no-store", beforeActivation.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(beforeJson["data"]!["found"]!.GetValue<bool>());
        Assert.Equal("Standing Desk", beforeJson["data"]!["productName"]!.GetValue<string>());
        Assert.DoesNotContain("customer@example.com", beforeBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORD-WARRANTY-001", beforeBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Identifier, beforeBody, StringComparison.Ordinal);

        var pendingEmailsBeforeActivation = await fixture.ExecuteDbAsync(dbContext =>
            dbContext.CustomerEmailOutboxMessages.CountAsync(message => message.SentAt == null));
        customerClient.UseBearerToken(customerToken);
        using var activation = await customerClient.PostAsJsonAsync("/api/customer/warranties/activate", new { identifier = Identifier });
        var activationJson = await activation.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
        Assert.Equal((int)WarrantyEntitlementStatus.Active, activationJson["data"]!["status"]!.GetValue<int>());
        Assert.Contains("no-store", activation.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);

        var persisted = await fixture.ExecuteDbAsync(async dbContext => new
        {
            Status = await dbContext.WarrantyEntitlements.Select(entitlement => entitlement.Status).SingleAsync(),
            SnapshotCount = await dbContext.WarrantyCoverageSnapshots.CountAsync(),
            PendingEmailCount = await dbContext.CustomerEmailOutboxMessages.CountAsync(message => message.SentAt == null)
        });
        Assert.Equal(WarrantyEntitlementStatus.Active, persisted.Status);
        Assert.Equal(2, persisted.SnapshotCount);
        Assert.Equal(pendingEmailsBeforeActivation + 1, persisted.PendingEmailCount);
    }

    [Fact]
    public async Task NonOwnerActivation_ReturnsGenericNotFoundAndDoesNotMutate()
    {
        await fixture.ResetDatabaseAsync();
        using var ownerClient = fixture.CreateClient();
        await ownerClient.RegisterCustomerAsync();
        var ownerId = await fixture.ExecuteDbAsync(dbContext => dbContext.Customers
            .Where(customer => customer.Email == "customer@example.com")
            .Select(customer => customer.Id)
            .SingleAsync());
        await SeedPendingWarrantyAsync(ownerId);

        using var attackerClient = fixture.CreateClient();
        using var attackerRegistration = await attackerClient.PostAsJsonAsync("/api/customer/auth/register", new
        {
            fullName = "Attacker",
            phoneNumber = "0900000001",
            email = "attacker@example.com",
            password = "attacker-password"
        });
        var attackerToken = (await attackerRegistration.ReadJsonAsync())["data"]!["accessToken"]!.GetValue<string>();
        Assert.Equal(HttpStatusCode.Created, attackerRegistration.StatusCode);
        var emailCountBeforeActivation = await fixture.ExecuteDbAsync(dbContext => dbContext.CustomerEmailOutboxMessages.CountAsync());
        attackerClient.UseBearerToken(attackerToken);
        using var response = await attackerClient.PostAsJsonAsync("/api/customer/warranties/activate", new { identifier = Identifier });
        var body = (await response.ReadJsonAsync()).ToJsonString();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(Identifier, body, StringComparison.Ordinal);
        var state = await fixture.ExecuteDbAsync(async dbContext => new
        {
            Status = await dbContext.WarrantyEntitlements.Select(entitlement => entitlement.Status).SingleAsync(),
            EmailCount = await dbContext.CustomerEmailOutboxMessages.CountAsync()
        });
        Assert.Equal(WarrantyEntitlementStatus.PendingActivation, state.Status);
        Assert.Equal(emailCountBeforeActivation, state.EmailCount);
    }

    private async Task SeedPendingWarrantyAsync(Guid customerId)
    {
        await fixture.SeedAsync(async dbContext =>
        {
            var catalog = TestData.CreateVisibleCatalog();
            dbContext.AddRange(catalog.Category, catalog.Product, catalog.Variant, catalog.Image, catalog.Specification);

            var order = new Order(
                Guid.NewGuid(),
                "ORD-WARRANTY-001",
                customerId,
                "Nguyen Van A",
                "0900000000",
                "customer@example.com",
                "123 Shipping Street",
                null,
                PaymentMethod.Cod,
                "VND",
                1m);
            var item = order.AddItem(Guid.NewGuid(), catalog.Variant.Id, "Standing Desk", catalog.Variant.Sku, 100m, 1, false);
            order.RecordCreated(Guid.NewGuid(), null, "admin");
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "admin");
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "admin");
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "admin");
            order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, "admin");

            var now = DateTimeOffset.UtcNow;
            var plan = new WarrantyPlan(Guid.NewGuid(), "DESK-V1", "Standing desk warranty", 60, "v1", now.AddDays(-1), null);
            var frameCoverage = plan.AddCoverage(Guid.NewGuid(), "FRAME", "Frame", 60, 0);
            var motorCoverage = plan.AddCoverage(Guid.NewGuid(), "MOTOR", "Motor", 36, 1);
            var batch = new WarrantyImportBatch(Guid.NewGuid(), "safe-checksum", "admin", 1, now);
            batch.Complete(1, 0, now);
            var unit = new SerializedProductUnit(
                Guid.NewGuid(),
                catalog.Variant.Id,
                WarrantyIdentifierType.Serial,
                1,
                Fingerprint(Identifier),
                "******-001",
                batch.Id,
                now);
            unit.AssignToOrderItem(item.Id, now);
            var entitlement = new WarrantyEntitlement(Guid.NewGuid(), unit.Id, plan.Id, order.Id, item.Id, customerId, now);

            dbContext.AddRange(order, plan, frameCoverage, motorCoverage, batch, unit, entitlement);
            await Task.CompletedTask;
        });
    }

    private static string Fingerprint(string normalizedIdentifier)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(IdentifierKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"v1|{WarrantyIdentifierType.Serial}|{normalizedIdentifier}"))).ToLowerInvariant();
    }
}
