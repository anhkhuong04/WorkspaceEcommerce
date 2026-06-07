namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed record OrderItemDto(
    Guid Id,
    Guid ProductVariantId,
    string ProductNameSnapshot,
    string SkuSnapshot,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    bool RequiresInstallation);
