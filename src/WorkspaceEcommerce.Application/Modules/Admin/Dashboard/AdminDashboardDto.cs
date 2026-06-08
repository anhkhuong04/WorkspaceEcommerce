namespace WorkspaceEcommerce.Application.Modules.Admin.Dashboard;

public sealed record AdminDashboardDto(
    int TotalOrders,
    decimal TotalRevenue,
    int NewOrders,
    IReadOnlyCollection<LowStockProductVariantDto> LowStockVariants);
