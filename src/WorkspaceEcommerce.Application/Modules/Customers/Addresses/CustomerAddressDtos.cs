namespace WorkspaceEcommerce.Application.Modules.Customers.Addresses;

public sealed record CustomerAddressDto(
    Guid Id,
    string Label,
    string ContactName,
    string ContactPhone,
    string Street,
    string Ward,
    string Province,
    bool IsDefault);

public sealed record SaveCustomerAddressRequest(
    string Label,
    string ContactName,
    string ContactPhone,
    string Street,
    string Ward,
    string Province);

public sealed record CustomerAccountStatsDto(
    int TotalOrders,
    int PendingOrders,
    int ShippingOrders,
    int TotalRewardPoints);

public sealed record CustomerLoginHistoryDto(
    Guid Id,
    DateTimeOffset LoginTime,
    string IpAddress,
    string UserAgent,
    bool Success);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);

public sealed record Toggle2FARequest(
    bool Enable,
    string? Code);
