namespace WorkspaceEcommerce.Application.Abstractions.Shipment;

public sealed class ShippingQuoteRequest
{
    public string? ExternalOrderId { get; init; }

    public ShippingAddress DeliveryAddress { get; init; } = null!;

    public ShippingParcel Parcel { get; init; } = null!;

    public decimal GoodsValueAmount { get; init; }

    public decimal CodAmount { get; init; }
}

public sealed class ShippingAddress
{
    public string Street { get; init; } = string.Empty;

    public string Ward { get; init; } = string.Empty;

    public string Province { get; init; } = string.Empty;

    public string Country { get; init; } = "Vietnam";
}

public sealed class ShippingParcel
{
    public decimal WeightKg { get; init; }

    public decimal LengthCm { get; init; }

    public decimal WidthCm { get; init; }

    public decimal HeightCm { get; init; }
}

public sealed class ShippingQuoteResponse
{
    public string RouteType { get; init; } = string.Empty;

    public decimal TotalFeeAmount { get; init; }

    public decimal BaseFeeAmount { get; init; }

    public decimal ExtraWeightFeeAmount { get; init; }

    public decimal InsuranceFeeAmount { get; init; }

    public string Currency { get; init; } = "VND";
}

public sealed class CreateShipmentRequest
{
    public string ExternalOrderId { get; init; } = string.Empty;

    public ShipmentContact? Sender { get; init; }

    public ShipmentContact Receiver { get; init; } = null!;

    public ShippingAddress? PickupAddress { get; init; }

    public ShippingAddress DeliveryAddress { get; init; } = null!;

    public ShippingParcel Parcel { get; init; } = null!;

    public decimal GoodsValueAmount { get; init; }

    public decimal CodAmount { get; init; }

    public string? Note { get; init; }
}

public sealed class ShipmentContact
{
    public string Name { get; init; } = string.Empty;

    public string Phone { get; init; } = string.Empty;
}

public sealed class CreateShipmentResponse
{
    public Guid ShipmentId { get; init; }

    public string ExternalOrderId { get; init; } = string.Empty;

    public string TrackingCode { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public decimal ShippingFeeAmount { get; init; }

    public string Currency { get; init; } = "VND";
}

public sealed class TrackingResponse
{
    public string TrackingCode { get; init; } = string.Empty;

    public string ExternalOrderId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public decimal ShippingFeeAmount { get; init; }

    public TrackingTimelineEntry[] Timeline { get; init; } = [];
}

public sealed class TrackingTimelineEntry
{
    public string Status { get; init; } = string.Empty;

    public string? Note { get; init; }

    public DateTimeOffset ChangedAtUtc { get; init; }
}
