namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record LowStockProductVariantDto(
    Guid ProductId,
    string ProductName,
    Guid VariantId,
    string Sku,
    string VariantName,
    int StockQuantity,
    bool IsActive);
