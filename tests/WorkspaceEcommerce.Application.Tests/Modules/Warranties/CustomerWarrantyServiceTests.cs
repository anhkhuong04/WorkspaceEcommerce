using FluentValidation;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Notifications;
using WorkspaceEcommerce.Application.Abstractions.Warranties;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Warranties;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Modules.Catalog;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Domain.Modules.Warranties;

namespace WorkspaceEcommerce.Application.Tests.Modules.Warranties;

public sealed class CustomerWarrantyServiceTests
{
    [Fact]
    public async Task ActivateAsync_ForCompletedOwnedOrder_ActivatesOnceAndQueuesEmail()
    {
        var fixture = CreateFixture();
        var result = await fixture.Service.ActivateAsync(new ActivateWarrantyRequest { Identifier = "SERIAL-001" });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(WarrantyEntitlementStatus.Active, result.Value.Status);
        Assert.Single(fixture.EmailOutbox.Messages);
        Assert.Equal(1, fixture.DbContext.SaveChangesCallCount);

        var retry = await fixture.Service.ActivateAsync(new ActivateWarrantyRequest { Identifier = "SERIAL-001" });
        Assert.True(retry.IsSuccess);
        Assert.Equal(WarrantyEntitlementStatus.Active, retry.Value!.Status);
        Assert.Single(fixture.EmailOutbox.Messages);
    }

    [Fact]
    public async Task ActivateAsync_ForAnotherCustomer_DoesNotDiscloseOrMutateWarranty()
    {
        var fixture = CreateFixture(currentCustomerId: Guid.NewGuid());

        var result = await fixture.Service.ActivateAsync(new ActivateWarrantyRequest { Identifier = "SERIAL-001" });

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal(WarrantyEntitlementStatus.PendingActivation, fixture.Entitlement.Status);
        Assert.Empty(fixture.EmailOutbox.Messages);
    }

    [Fact]
    public async Task ActivateAsync_AfterDeadline_ReturnsValidationAndLeavesPending()
    {
        var fixture = CreateFixture(completedDaysAgo: 61);

        var result = await fixture.Service.ActivateAsync(new ActivateWarrantyRequest { Identifier = "SERIAL-001" });

        Assert.Equal(ResultStatus.Validation, result.Status);
        Assert.Contains("activation window has expired", result.Errors.Single(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WarrantyEntitlementStatus.PendingActivation, fixture.Entitlement.Status);
    }

    private static Fixture CreateFixture(Guid? currentCustomerId = null, int completedDaysAgo = 0)
    {
        var ownerId = Guid.NewGuid();
        var customerId = currentCustomerId ?? ownerId;
        var variant = new ProductVariant(Guid.NewGuid(), Guid.NewGuid(), "CHAIR-001", "Black", null, null, 100m, null, 10, false);
        var order = new Order(Guid.NewGuid(), "ORD-WARRANTY-001", ownerId, "Customer", "0900000000", "customer@example.com", "Address", null, PaymentMethod.Cod, "VND", 1m);
        var item = order.AddItem(Guid.NewGuid(), variant.Id, "Ergonomic Chair", variant.Sku, 100m, 1, false);
        order.RecordCreated(Guid.NewGuid(), null, null);
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Confirmed, null, "admin");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Processing, null, "admin");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Shipping, null, "admin");
        order.ChangeStatus(Guid.NewGuid(), OrderStatus.Completed, null, "admin");
        if (completedDaysAgo > 0)
        {
            SetPrivateProperty(order, nameof(Order.CompletedAt), DateTimeOffset.UtcNow.AddDays(-completedDaysAgo));
        }

        var plan = new WarrantyPlan(Guid.NewGuid(), "CHAIR-V1", "Chair warranty", 60, "v1", DateTimeOffset.UtcNow.AddDays(-90), null);
        plan.AddCoverage(Guid.NewGuid(), "FRAME", "Frame", 60, 0);
        var batch = new WarrantyImportBatch(Guid.NewGuid(), "checksum", "admin", 1, DateTimeOffset.UtcNow);
        batch.Complete(1, 0, DateTimeOffset.UtcNow);
        var unit = new SerializedProductUnit(Guid.NewGuid(), variant.Id, WarrantyIdentifierType.Serial, 1, "SERIAL-001", "******L-001", batch.Id, DateTimeOffset.UtcNow);
        unit.AssignToOrderItem(item.Id, DateTimeOffset.UtcNow);
        var entitlement = new WarrantyEntitlement(Guid.NewGuid(), unit.Id, plan.Id, order.Id, item.Id, ownerId, DateTimeOffset.UtcNow);
        var dbContext = new FakeAppDbContext();
        dbContext.Seed(variant);
        dbContext.Seed(order);
        dbContext.Seed(plan);
        dbContext.Seed(batch);
        dbContext.Seed(unit);
        dbContext.Seed(entitlement);
        var outbox = new StubEmailOutbox();
        var service = new CustomerWarrantyService(
            dbContext,
            new StubCurrentCustomerContext(customerId),
            new StubIdentifierProtector(),
            outbox,
            new WarrantyOptions { Enabled = true, ActivationEnabled = true, IdentifierKeyVersion = 1, IdentifierHmacKey = "test-key-not-used-by-stub-1234567890" },
            TimeProvider.System,
            new ActivateWarrantyRequestValidator());
        return new Fixture(service, dbContext, entitlement, outbox);
    }

    private static void SetPrivateProperty<T>(object target, string name, T value)
    {
        typeof(Order).GetProperty(name)!.SetValue(target, value);
    }

    private sealed record Fixture(CustomerWarrantyService Service, FakeAppDbContext DbContext, WarrantyEntitlement Entitlement, StubEmailOutbox EmailOutbox);

    private sealed class StubCurrentCustomerContext(Guid customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;
        public string? Email => "customer@example.com";
    }

    private sealed class StubIdentifierProtector : IWarrantyIdentifierProtector
    {
        public WarrantyIdentifier Normalize(WarrantyIdentifierType? requestedType, string identifier) =>
            new(requestedType ?? WarrantyIdentifierType.Serial, identifier.Trim().ToUpperInvariant(), "******L-001");

        public string CreateFingerprint(WarrantyIdentifierType identifierType, string normalizedIdentifier, int keyVersion) => normalizedIdentifier;
    }

    private sealed class StubEmailOutbox : ICustomerEmailOutbox
    {
        public List<CustomerEmailMessage> Messages { get; } = [];
        public void Enqueue(CustomerEmailMessage message) => Messages.Add(message);
    }
}
