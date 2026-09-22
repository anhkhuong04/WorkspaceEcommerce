using System.Text;
using WorkspaceEcommerce.Application.Modules.Ordering.Receipts;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Configuration;
using WorkspaceEcommerce.Infrastructure.Documents;

namespace WorkspaceEcommerce.Infrastructure.Tests.Documents;

public sealed class QuestPdfOrderReceiptRendererTests
{
    [Fact]
    public void Constructor_WithUnsupportedLicense_ThrowsConfigurationError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new QuestPdfOrderReceiptRenderer(new OrderReceiptOptions { QuestPdfLicense = "Unknown" }));

        Assert.Contains("QuestPdfLicense", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    public void Render_CreatesPdfForSupportedLanguages(string language)
    {
        var renderer = new QuestPdfOrderReceiptRenderer(new OrderReceiptOptions
        {
            SellerName = "Workspace Ecommerce",
            QuestPdfLicense = "Community"
        });

        var content = renderer.Render(CreateSnapshot(language));

        Assert.True(content.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(content, 0, 4));
    }

    [Fact]
    public void Render_WithProductImage_EmbedsImageInPdf()
    {
        var renderer = new QuestPdfOrderReceiptRenderer(new OrderReceiptOptions { QuestPdfLicense = "Community" });
        var image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAF/gL+Y3P7WQAAAABJRU5ErkJggg==");
        var snapshot = CreateSnapshot("en") with
        {
            Items = [CreateSnapshot("en").Items.Single() with { ProductImage = image }]
        };

        var content = renderer.Render(snapshot);

        Assert.True(content.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(content, 0, 4));
    }

    private static OrderReceiptSnapshot CreateSnapshot(string language) => new(
        language,
        "ORD-RECEIPT-001",
        "Nguyễn Văn An",
        "0900000000",
        "an@example.com",
        "123 Nguyễn Huệ, Phường Bến Nghé, TP. Hồ Chí Minh",
        "Giao hàng trong giờ hành chính.",
        "WELCOME",
        "Ưu đãi chào mừng",
        2_500_000m,
        30_000m,
        100_000m,
        2_430_000m,
        "VND",
        OrderStatus.Confirmed,
        PaymentMethod.VNPay,
        PaymentStatus.Paid,
        new DateTimeOffset(2026, 8, 28, 3, 30, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 28, 3, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 8, 28, 4, 0, 0, TimeSpan.Zero),
        [
            new OrderReceiptLineSnapshot(
                "Bàn làm việc nâng hạ",
                null,
                "DESK-001",
                2_500_000m,
                1,
                2_500_000m,
                true)
        ]);
}
