namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record AdminOrderImportPreviewDto(
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    IReadOnlyCollection<AdminOrderImportRowResultDto> Rows);

public sealed record AdminOrderImportCommitResultDto(
    int CreatedOrders,
    IReadOnlyCollection<AdminOrderImportCreatedOrderDto> Orders,
    AdminOrderImportPreviewDto Preview);

public sealed record AdminOrderImportCreatedOrderDto(
    Guid Id,
    string OrderCode,
    string ExternalOrderCode);

public sealed record AdminOrderImportRowResultDto(
    int RowNumber,
    string ExternalOrderCode,
    string Sku,
    int? Quantity,
    bool IsValid,
    IReadOnlyCollection<string> Errors);

internal sealed record AdminOrderImportFileRow(
    int RowNumber,
    string ExternalOrderCode,
    string CustomerName,
    string CustomerPhone,
    string? CustomerEmail,
    string ShippingAddress,
    string ShippingStreet,
    string ShippingWard,
    string ShippingProvince,
    string PaymentMethod,
    string Sku,
    string Quantity,
    string? UnitPrice,
    string? ShippingFee,
    string? Note);
