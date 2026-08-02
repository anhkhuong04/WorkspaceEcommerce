using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed record ShipmentTrackingDto(
    Guid OrderId,
    string OrderCode,
    OrderStatus OrderStatus,
    Guid? ShipmentId,
    string? TrackingCode,
    string? Provider,
    string? ProviderStatus,
    decimal? ShippingFeeAmount,
    string? Currency,
    DateTimeOffset? LastSyncedAtUtc,
    DateTimeOffset? LastEventAtUtc,
    bool CanRetry,
    bool CanRefresh,
    bool CanCancel,
    string? LastCommandError,
    IReadOnlyCollection<ShipmentTimelineEntryDto> Timeline);

public sealed record ShipmentTimelineEntryDto(
    Guid Id,
    string ProviderStatus,
    string? Note,
    DateTimeOffset ChangedAtUtc,
    string Source);

public sealed record ShipmentWebhookPayload(
    Guid EventId,
    string Event,
    string TrackingCode,
    string ExternalOrderId,
    string Status,
    DateTimeOffset ChangedAtUtc);

public sealed record ShipmentWebhookResult(bool IsDuplicate, bool OrderUpdated, bool ShipmentUpdated);
