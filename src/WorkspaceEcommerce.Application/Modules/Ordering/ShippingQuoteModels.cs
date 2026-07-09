namespace WorkspaceEcommerce.Application.Modules.Ordering;

public sealed class GetShippingQuoteRequest
{
    public string SessionId { get; init; } = string.Empty;

    public string Street { get; init; } = string.Empty;

    public string Ward { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;
}

public sealed class GetShippingQuoteResponse(
    decimal TotalFeeAmount,
    decimal BaseFeeAmount,
    decimal ExtraWeightFeeAmount,
    decimal InsuranceFeeAmount,
    string RouteType,
    string Currency);
