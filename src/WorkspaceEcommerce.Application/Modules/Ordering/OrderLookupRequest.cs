namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class OrderLookupRequest
{
    public string OrderCode { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;
}
