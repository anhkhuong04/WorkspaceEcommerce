using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Documents;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering;
using WorkspaceEcommerce.Application.Modules.Ordering.Receipts;
using WorkspaceEcommerce.Application.Tests.Common.Fakes;
using WorkspaceEcommerce.Domain.Common;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Tests.Modules.Ordering;

public sealed class OrderReceiptServiceTests
{
    private static readonly DateTimeOffset GeneratedAt = new(2026, 8, 28, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateForGuestAsync_WithMatchingCodeAndPhone_RendersSnapshot()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder(customerId: null);
        dbContext.Seed(order);
        var renderer = new CapturingRenderer();
        var service = CreateService(dbContext, renderer, customerId: null, language: "vi");

        var result = await service.GenerateForGuestAsync(new OrderLookupRequest
        {
            OrderCode = " ord-receipt-001 ",
            Phone = "0900000000"
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("application/pdf", result.Value!.ContentType);
        Assert.Equal("order-receipt-ORD-RECEIPT-001.pdf", result.Value.FileName);
        Assert.NotNull(renderer.Snapshot);
        Assert.Equal("vi", renderer.Snapshot.Language);
        Assert.Equal(GeneratedAt, renderer.Snapshot.GeneratedAt);
        Assert.Equal(2_500_000m, renderer.Snapshot.TotalAmount);
        Assert.Single(renderer.Snapshot.Items);
    }

    [Fact]
    public async Task GenerateForCustomerAsync_DoesNotExposeAnotherCustomersOrder()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder(Guid.NewGuid());
        dbContext.Seed(order);
        var renderer = new CapturingRenderer();
        var service = CreateService(dbContext, renderer, Guid.NewGuid(), "en");

        var result = await service.GenerateForCustomerAsync(order.Id);

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Null(renderer.Snapshot);
    }

    [Fact]
    public async Task GenerateForGuestAsync_EmbedsResolvedProductImageInRenderSnapshot()
    {
        var dbContext = new FakeAppDbContext();
        var order = CreateOrder(customerId: null, productImageUrl: "/media/products/desk.png");
        dbContext.Seed(order);
        var renderer = new CapturingRenderer();
        var image = new byte[] { 1, 2, 3 };
        var service = CreateService(
            dbContext,
            renderer,
            customerId: null,
            language: "en",
            imageResolver: new StubImageResolver("/media/products/desk.png", image));

        var result = await service.GenerateForGuestAsync(new OrderLookupRequest
        {
            OrderCode = order.OrderCode,
            Phone = order.CustomerPhone
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(image, Assert.Single(renderer.Snapshot!.Items).ProductImage);
    }

    private static OrderReceiptService CreateService(
        FakeAppDbContext dbContext,
        CapturingRenderer renderer,
        Guid? customerId,
        string language,
        IOrderReceiptImageResolver? imageResolver = null) => new(
            dbContext,
            new StubCurrentCustomerContext(customerId),
            new StubLanguageProvider(language),
            new OrderLookupRequestValidator(),
            renderer,
            new StubTimeProvider(GeneratedAt),
            imageResolver);

    private static Order CreateOrder(Guid? customerId, string? productImageUrl = null)
    {
        var order = new Order(
            Guid.NewGuid(),
            "ORD-RECEIPT-001",
            customerId,
            "Nguyễn Văn An",
            "0900000000",
            "an@example.com",
            "123 Nguyễn Huệ, TP. Hồ Chí Minh",
            "Giao giờ hành chính",
            PaymentMethod.Cod,
            CommerceCurrency.Code,
            CommerceCurrency.BaseExchangeRate);
        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Bàn làm việc",
            "DESK-001",
            2_500_000m,
            1,
            false,
            productImageUrl);
        return order;
    }

    private sealed class CapturingRenderer : IOrderReceiptRenderer
    {
        public OrderReceiptSnapshot? Snapshot { get; private set; }

        public byte[] Render(OrderReceiptSnapshot receipt)
        {
            Snapshot = receipt;
            return "%PDF"u8.ToArray();
        }
    }

    private sealed class StubCurrentCustomerContext(Guid? customerId) : ICurrentCustomerContext
    {
        public Guid? CustomerId => customerId;
        public string? Email => null;
    }

    private sealed class StubLanguageProvider(string language) : ICurrentLanguageProvider
    {
        public string CurrentLanguage => language;
    }

    private sealed class StubTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubImageResolver(string imageUrl, byte[] image) : IOrderReceiptImageResolver
    {
        public Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
            IReadOnlyCollection<string> imageUrls,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<string, byte[]> result = imageUrls.Contains(imageUrl, StringComparer.Ordinal)
                ? new Dictionary<string, byte[]>(StringComparer.Ordinal) { [imageUrl] = image }
                : new Dictionary<string, byte[]>();
            return Task.FromResult(result);
        }
    }
}
