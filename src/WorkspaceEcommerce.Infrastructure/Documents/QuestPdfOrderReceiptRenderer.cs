using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using WorkspaceEcommerce.Application.Abstractions.Documents;
using WorkspaceEcommerce.Application.Modules.Ordering.Receipts;
using WorkspaceEcommerce.Domain.Modules.Ordering;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Documents;

internal sealed class QuestPdfOrderReceiptRenderer : IOrderReceiptRenderer
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly OrderReceiptOptions _options;

    public QuestPdfOrderReceiptRenderer(OrderReceiptOptions options)
    {
        _options = options;
        QuestPDF.Settings.License = ParseLicense(options.QuestPdfLicense);
        QuestPDF.Settings.UseEnvironmentFonts = false;
        QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = true;
    }

    public byte[] Render(OrderReceiptSnapshot receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        var labels = ReceiptLabels.For(receipt.Language);
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(10).FontColor("#1E293B"));

                page.Header().Element(container => ComposeHeader(container, receipt, labels));
                page.Content().PaddingTop(20).Element(container => ComposeContent(container, receipt, labels));
                page.Footer().PaddingTop(12).Element(container => ComposeFooter(container, receipt, labels));
            });
        }).GeneratePdf();
    }

    private void ComposeHeader(IContainer container, OrderReceiptSnapshot receipt, ReceiptLabels labels)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(_options.SellerName).FontSize(18).Bold().FontColor("#0F172A");
                    if (!string.IsNullOrWhiteSpace(_options.SupportEmail))
                    {
                        left.Item().Text(_options.SupportEmail).FontSize(9).FontColor("#64748B");
                    }

                    if (!string.IsNullOrWhiteSpace(_options.SupportPhone))
                    {
                        left.Item().Text(_options.SupportPhone).FontSize(9).FontColor("#64748B");
                    }
                });

                row.RelativeItem().AlignRight().Column(right =>
                {
                    right.Item().AlignRight().Text(labels.Title).FontSize(20).Bold().FontColor("#E11D48");
                    right.Item().AlignRight().Text($"{labels.OrderCode}: {receipt.OrderCode}").Bold();
                    right.Item().AlignRight().Text($"{labels.OrderedAt}: {FormatDate(receipt.OrderedAt, receipt.Language)}")
                        .FontSize(9).FontColor("#64748B");
                });
            });

            column.Item().PaddingTop(14).Background("#FFF1F2").Border(1).BorderColor("#FECDD3")
                .Padding(10).Text(labels.Disclaimer).Bold().FontColor("#9F1239");
        });
    }

    private static void ComposeContent(IContainer container, OrderReceiptSnapshot receipt, ReceiptLabels labels)
    {
        container.Column(column =>
        {
            column.Spacing(18);
            column.Item().Row(row =>
            {
                row.RelativeItem().Element(card => ComposeInfoCard(card, labels.Recipient, new[]
                {
                    (labels.CustomerName, receipt.CustomerName),
                    (labels.Phone, receipt.CustomerPhone),
                    (labels.Email, receipt.CustomerEmail ?? "-"),
                    (labels.ShippingAddress, receipt.ShippingAddress)
                }));

                row.ConstantItem(16);
                row.RelativeItem().Element(card => ComposeInfoCard(card, labels.OrderInformation, new[]
                {
                    (labels.OrderStatusLabel, labels.Status(receipt.Status)),
                    (labels.PaymentMethodLabel, labels.PaymentMethodText(receipt.PaymentMethod)),
                    (labels.PaymentStatusLabel, labels.PaymentStatusText(receipt.PaymentStatus)),
                    (labels.PaidAt, receipt.PaidAt.HasValue ? FormatDate(receipt.PaidAt.Value, receipt.Language) : "-")
                }));
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(48);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(42);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    HeaderCell(header.Cell(), labels.Image);
                    HeaderCell(header.Cell(), labels.Product);
                    HeaderCell(header.Cell(), labels.UnitPrice, true);
                    HeaderCell(header.Cell(), labels.Quantity, true);
                    HeaderCell(header.Cell(), labels.LineTotal, true);
                });

                foreach (var item in receipt.Items)
                {
                    ImageCell(table.Cell(), item.ProductImage, labels.NoImage);
                    BodyCell(table.Cell(), string.IsNullOrWhiteSpace(item.Sku)
                        ? item.ProductName
                        : $"{item.ProductName}\nSKU: {item.Sku}" + (item.RequiresInstallation ? $"\n{labels.InstallationRequired}" : string.Empty));
                    BodyCell(table.Cell(), FormatMoney(item.UnitPrice, receipt.CurrencyCode), true);
                    BodyCell(table.Cell(), item.Quantity.ToString(CultureInfo.InvariantCulture), true);
                    BodyCell(table.Cell(), FormatMoney(item.LineTotal, receipt.CurrencyCode), true);
                }
            });

            column.Item().AlignRight().Width(260).Column(totals =>
            {
                totals.Spacing(7);
                totals.Item().Element(row => TotalRow(row, labels.Subtotal, FormatMoney(receipt.Subtotal, receipt.CurrencyCode)));
                totals.Item().Element(row => TotalRow(row, labels.ShippingFee, FormatMoney(receipt.ShippingFee, receipt.CurrencyCode)));
                if (receipt.DiscountAmount > 0)
                {
                    var discountLabel = string.IsNullOrWhiteSpace(receipt.CouponCode)
                        ? labels.Discount
                        : $"{labels.Discount} ({receipt.CouponCode})";
                    totals.Item().Element(row => TotalRow(row, discountLabel, $"-{FormatMoney(receipt.DiscountAmount, receipt.CurrencyCode)}", "#047857"));
                }

                totals.Item().PaddingTop(7).BorderTop(1).BorderColor("#CBD5E1")
                    .Element(row => TotalRow(row, labels.Total, FormatMoney(receipt.TotalAmount, receipt.CurrencyCode), "#BE123C", true));
            });

            if (!string.IsNullOrWhiteSpace(receipt.CustomerNote))
            {
                column.Item().Background("#F8FAFC").Border(1).BorderColor("#E2E8F0").Padding(12).Column(note =>
                {
                    note.Item().Text(labels.CustomerNote).Bold().FontColor("#475569");
                    note.Item().PaddingTop(3).Text(receipt.CustomerNote);
                });
            }

            column.Item().Text(labels.ThankYou).AlignCenter().Italic().FontColor("#64748B");
        });
    }

    private static void ComposeFooter(IContainer container, OrderReceiptSnapshot receipt, ReceiptLabels labels)
    {
        container.BorderTop(1).BorderColor("#E2E8F0").PaddingTop(8).Row(row =>
        {
            row.RelativeItem().Text($"{labels.GeneratedAt}: {FormatDate(receipt.GeneratedAt, receipt.Language)}")
                .FontSize(8).FontColor("#64748B");
            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(8).FontColor("#64748B"));
                text.Span(labels.Page + " ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void ComposeInfoCard(
        IContainer container,
        string title,
        IEnumerable<(string Label, string Value)> values)
    {
        container.Border(1).BorderColor("#E2E8F0").Padding(12).Column(column =>
        {
            column.Spacing(5);
            column.Item().Text(title).Bold().FontColor("#BE123C");
            foreach (var (label, value) in values)
            {
                column.Item().Text(text =>
                {
                    text.Span(label + ": ").SemiBold().FontColor("#64748B");
                    text.Span(value);
                });
            }
        });
    }

    private static void HeaderCell(IContainer container, string text, bool alignRight = false)
    {
        var cell = container.Background("#0F172A").PaddingVertical(7).PaddingHorizontal(6);
        if (alignRight)
        {
            cell.AlignRight().Text(text).Bold().FontColor(Colors.White);
        }
        else
        {
            cell.Text(text).Bold().FontColor(Colors.White);
        }
    }

    private static void BodyCell(IContainer container, string text, bool alignRight = false)
    {
        var cell = container.BorderBottom(1).BorderColor("#E2E8F0").PaddingVertical(8).PaddingHorizontal(6);
        if (alignRight)
        {
            cell.AlignRight().Text(text);
        }
        else
        {
            cell.Text(text);
        }
    }

    private static void ImageCell(IContainer container, byte[]? image, string noImageLabel)
    {
        var cell = container.BorderBottom(1).BorderColor("#E2E8F0").Padding(4).Height(48).AlignCenter().AlignMiddle();
        if (image is { Length: > 0 })
        {
            cell.Image(image).FitArea();
            return;
        }

        cell.Text(noImageLabel).FontSize(7).FontColor("#94A3B8");
    }

    private static void TotalRow(IContainer container, string label, string value, string? color = null, bool bold = false)
    {
        container.Row(row =>
        {
            var labelText = row.RelativeItem().Text(label);
            var valueText = row.RelativeItem().AlignRight().Text(value);
            if (bold)
            {
                labelText.Bold().FontSize(12);
                valueText.Bold().FontSize(12);
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                labelText.FontColor(color);
                valueText.FontColor(color);
            }
        });
    }

    private static string FormatMoney(decimal amount, string currencyCode) =>
        $"{amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} {currencyCode}";

    private static string FormatDate(DateTimeOffset value, string language)
    {
        var vietnamTime = value.ToOffset(VietnamOffset);
        return language == "vi"
            ? vietnamTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) + " (UTC+7)"
            : vietnamTime.ToString("dd MMM yyyy HH:mm", CultureInfo.GetCultureInfo("en-US")) + " (UTC+7)";
    }

    private static LicenseType ParseLicense(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            null or "" or "community" => LicenseType.Community,
            "professional" => LicenseType.Professional,
            "enterprise" => LicenseType.Enterprise,
            _ => throw new InvalidOperationException(
                "OrderReceipt:QuestPdfLicense must be Community, Professional, or Enterprise.")
        };
    }

    private sealed record ReceiptLabels(
        string Title,
        string Disclaimer,
        string OrderCode,
        string OrderedAt,
        string Recipient,
        string CustomerName,
        string Phone,
        string Email,
        string ShippingAddress,
        string OrderInformation,
        string OrderStatusLabel,
        string PaymentMethodLabel,
        string PaymentStatusLabel,
        string PaidAt,
        string Image,
        string Product,
        string UnitPrice,
        string Quantity,
        string LineTotal,
        string InstallationRequired,
        string Subtotal,
        string ShippingFee,
        string Discount,
        string Total,
        string CustomerNote,
        string ThankYou,
        string GeneratedAt,
        string Page,
        string NoImage,
        bool Vietnamese)
    {
        public static ReceiptLabels For(string language) => language == "vi"
            ? new ReceiptLabels(
                "PHIẾU XÁC NHẬN ĐƠN HÀNG",
                "Đây là phiếu xác nhận mua hàng/biên nhận, không phải hóa đơn GTGT và không dùng để kê khai thuế.",
                "Mã đơn hàng", "Ngày đặt hàng", "Thông tin người nhận", "Họ và tên", "Điện thoại", "Email",
                "Địa chỉ giao hàng", "Thông tin đơn hàng", "Trạng thái đơn hàng", "Phương thức thanh toán",
                "Trạng thái thanh toán", "Thanh toán lúc", "Ảnh", "Sản phẩm", "Đơn giá", "SL", "Thành tiền",
                "Yêu cầu lắp đặt", "Tạm tính", "Phí vận chuyển", "Giảm giá", "Tổng cộng", "Ghi chú của khách hàng",
                "Cảm ơn bạn đã mua sắm tại Workspace Ecommerce.", "Tạo lúc", "Trang", "Không có ảnh", true)
            : new ReceiptLabels(
                "ORDER RECEIPT",
                "This is an order confirmation/receipt. It is not a VAT invoice and cannot be used for tax declaration.",
                "Order code", "Ordered at", "Recipient details", "Full name", "Phone", "Email",
                "Shipping address", "Order information", "Order status", "Payment method", "Payment status", "Paid at",
                "Image", "Product", "Unit price", "Qty", "Line total", "Installation required", "Subtotal", "Shipping fee",
                "Discount", "Total", "Customer note", "Thank you for shopping with Workspace Ecommerce.", "Generated at", "Page", "No image", false);

        public string Status(OrderStatus status) => (Vietnamese, status) switch
        {
            (true, OrderStatus.Pending) => "Chờ xác nhận",
            (true, OrderStatus.Confirmed) => "Đã xác nhận",
            (true, OrderStatus.Processing) => "Đang xử lý",
            (true, OrderStatus.Shipping) => "Đang giao hàng",
            (true, OrderStatus.Completed) => "Hoàn tất",
            (true, OrderStatus.FailedDelivery) => "Giao hàng thất bại",
            (true, OrderStatus.Cancelled) => "Đã hủy",
            (true, OrderStatus.Returned) => "Đã trả hàng",
            (_, OrderStatus.Pending) => "Pending",
            (_, OrderStatus.Confirmed) => "Confirmed",
            (_, OrderStatus.Processing) => "Processing",
            (_, OrderStatus.Shipping) => "Shipping",
            (_, OrderStatus.Completed) => "Completed",
            (_, OrderStatus.FailedDelivery) => "Failed delivery",
            (_, OrderStatus.Cancelled) => "Cancelled",
            (_, OrderStatus.Returned) => "Returned",
            _ => status.ToString()
        };

        public string PaymentMethodText(PaymentMethod method) => (Vietnamese, method) switch
        {
            (true, PaymentMethod.Cod) => "Thanh toán khi nhận hàng (COD)",
            (true, PaymentMethod.ManualBankTransfer) => "Chuyển khoản ngân hàng",
            (true, PaymentMethod.VNPay) => "VNPay",
            (_, PaymentMethod.Cod) => "Cash on delivery (COD)",
            (_, PaymentMethod.ManualBankTransfer) => "Bank transfer",
            (_, PaymentMethod.VNPay) => "VNPay",
            _ => method.ToString()
        };

        public string PaymentStatusText(PaymentStatus status) => (Vietnamese, status) switch
        {
            (true, PaymentStatus.Unpaid) => "Chưa thanh toán",
            (true, PaymentStatus.Pending) => "Đang chờ",
            (true, PaymentStatus.Paid) => "Đã thanh toán",
            (true, PaymentStatus.Failed) => "Thất bại",
            (true, PaymentStatus.Cancelled) => "Đã hủy",
            (true, PaymentStatus.RefundPending) => "Đang chờ hoàn tiền",
            (_, PaymentStatus.Unpaid) => "Unpaid",
            (_, PaymentStatus.Pending) => "Pending",
            (_, PaymentStatus.Paid) => "Paid",
            (_, PaymentStatus.Failed) => "Failed",
            (_, PaymentStatus.Cancelled) => "Cancelled",
            (_, PaymentStatus.RefundPending) => "Refund pending",
            _ => status.ToString()
        };
    }
}
