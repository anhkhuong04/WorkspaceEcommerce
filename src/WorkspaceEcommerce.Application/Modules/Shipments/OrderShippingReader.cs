using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Domain.Modules.Shipments;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

public sealed record OrderShippingSummaryDto(Guid OrderId, ShippingMethod Method, decimal CustomerCharge,
    Guid? LocalShipmentId, Guid? ProviderShipmentId, string? TrackingCode, string? Status);
public sealed record OrderShippingDetailsDto(ShippingMethod Method, decimal CustomerCharge, string Currency,
    int WeightGrams, Guid? RatePlanId, long? RateRevision, string? ZoneCode, ShippingDestination? Destination,
    Guid? LocalShipmentId, Guid? ProviderShipmentId, string? TrackingCode, string? Status);
public interface IOrderShippingReader
{
    Task<OrderShippingDetailsDto?> GetAsync(Guid orderId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, OrderShippingSummaryDto>> GetManyAsync(Guid[] orderIds, CancellationToken ct);
}
internal sealed class OrderShippingReader(IShippingCheckoutStore shipping, IOrderReadStore orders) : IOrderShippingReader
{
    public async Task<OrderShippingDetailsDto?> GetAsync(Guid orderId, CancellationToken ct)
    {
        var row = await (from selection in shipping.Selections.AsNoTracking().Where(x => x.OrderId == orderId)
                         join shipment in orders.OrderShipments.AsNoTracking() on selection.OrderId equals shipment.OrderId into matches
                         from shipment in matches.DefaultIfEmpty()
                         select new { selection.Method, selection.CustomerCharge, selection.WeightGrams, selection.RatePlanId, selection.SnapshotJson,
                             LocalId = selection.Method == ShippingMethod.WorkspaceEC && shipment != null ? (Guid?)shipment.Id : null,
                             ProviderId = shipment == null ? null : shipment.ProviderShipmentId,
                             Tracking = shipment == null ? null : shipment.TrackingCode, Status = shipment == null ? null : shipment.ProviderStatus }).SingleOrDefaultAsync(ct);
        if (row is null) return null; // Absence deliberately means legacy.
        using var json = JsonDocument.Parse(row.SnapshotJson);
        var known = json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("Version", out var version)
            && version.TryGetInt32(out var value) && value == 1;
        var snapshot = known ? JsonSerializer.Deserialize<ShippingInputSnapshot>(row.SnapshotJson) : null;
        return new(row.Method, row.CustomerCharge, "VND", row.WeightGrams, row.RatePlanId, snapshot?.RateRevision,
            snapshot?.ZoneCode, snapshot?.Destination, row.LocalId, row.ProviderId, row.Tracking, row.Status);
    }
    public async Task<IReadOnlyDictionary<Guid, OrderShippingSummaryDto>> GetManyAsync(Guid[] orderIds, CancellationToken ct)
    {
        if (orderIds.Length == 0) return new Dictionary<Guid, OrderShippingSummaryDto>();
        if (orderIds.Length > 1000) throw new ArgumentOutOfRangeException(nameof(orderIds));
        var rows = await (from selection in shipping.Selections.AsNoTracking().Where(x => orderIds.Contains(x.OrderId))
                          join shipment in orders.OrderShipments.AsNoTracking() on selection.OrderId equals shipment.OrderId into matches
                          from shipment in matches.DefaultIfEmpty()
                          select new OrderShippingSummaryDto(selection.OrderId, selection.Method, selection.CustomerCharge,
                              selection.Method == ShippingMethod.WorkspaceEC && shipment != null ? (Guid?)shipment.Id : null,
                              shipment == null ? null : shipment.ProviderShipmentId, shipment == null ? null : shipment.TrackingCode,
                              shipment == null ? null : shipment.ProviderStatus)).ToArrayAsync(ct);
        return rows.ToDictionary(x => x.OrderId);
    }
}
