using Microsoft.Extensions.Logging;
using WorkspaceEcommerce.Application.Abstractions.Persistence;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Shipments;
using WorkspaceEcommerce.Domain.Modules.Ordering;

namespace WorkspaceEcommerce.Application.Modules.Ordering;

internal sealed class CheckoutShippingCoordinator(
    IShipmentService shipmentService,
    ICheckoutStore checkoutStore,
    ILogger logger)
{
    public async Task<Result<GetShippingQuoteResponse>> GetQuotePreviewAsync(
        GetShippingQuoteRequest request,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        try
        {
            var quoteResponse = await shipmentService.GetShippingQuoteAsync(new ShippingQuoteRequest
            {
                DeliveryAddress = new ShippingAddress
                {
                    Street = request.Street,
                    Ward = request.Ward,
                    Province = request.Province
                },
                Parcel = CheckoutCartBuilder.AggregateParcel(snapshots),
                GoodsValueAmount = snapshots.Sum(snapshot => snapshot.LineTotal),
                CodAmount = 0
            }, cancellationToken);

            return Result<GetShippingQuoteResponse>.Success(new GetShippingQuoteResponse(
                quoteResponse.TotalFeeAmount,
                quoteResponse.BaseFeeAmount,
                quoteResponse.ExtraWeightFeeAmount,
                quoteResponse.InsuranceFeeAmount,
                quoteResponse.RouteType,
                quoteResponse.Currency));
        }
        catch (HttpRequestException ex)
        {
            ShipmentIntegrationMetrics.RecordQuoteFailure();
            logger.LogError(ex, "Failed to get shipping quote from MiniLogistics");
            return Result<GetShippingQuoteResponse>.Failure("Could not calculate shipping fee. Please try again.");
        }
    }

    public async Task<Result<ShippingQuoteResponse>> GetQuoteForCheckoutAsync(
        CheckoutRequest request,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        decimal codAmount,
        CancellationToken cancellationToken)
    {
        try
        {
            var quoteResponse = await shipmentService.GetShippingQuoteAsync(new ShippingQuoteRequest
            {
                DeliveryAddress = BuildDeliveryAddress(request),
                Parcel = CheckoutCartBuilder.AggregateParcel(snapshots),
                GoodsValueAmount = snapshots.Sum(snapshot => snapshot.LineTotal),
                CodAmount = codAmount
            }, cancellationToken);

            return Result<ShippingQuoteResponse>.Success(quoteResponse);
        }
        catch (HttpRequestException ex)
        {
            ShipmentIntegrationMetrics.RecordQuoteFailure();
            logger.LogError(ex, "Failed to calculate shipping fee before VNPay checkout.");
            return Result<ShippingQuoteResponse>.Failure("Could not calculate shipping fee. Please try again.");
        }
    }

    public async Task RefreshShippingQuoteAfterCheckoutAsync(
        Order order,
        CheckoutRequest request,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        try
        {
            var codAmount = order.PaymentMethod == PaymentMethod.Cod ? order.TotalAmount : 0m;
            await ApplyShippingQuoteAsync(
                order,
                request,
                snapshots,
                codAmount,
                cancellationToken);
            checkoutStore.Update(order);
        }
        catch (HttpRequestException ex)
        {
            ShipmentIntegrationMetrics.RecordCreateFailure();
            logger.LogWarning(
                ex,
                "MiniLogistics quote refresh failed for order {OrderCode}; durable shipment creation remains queued.",
                order.OrderCode);
        }

        await checkoutStore.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyShippingQuoteAsync(
        Order order,
        CheckoutRequest request,
        IReadOnlyCollection<CheckoutItemSnapshot> snapshots,
        decimal codAmount,
        CancellationToken cancellationToken)
    {
        var quoteResponse = await shipmentService.GetShippingQuoteAsync(new ShippingQuoteRequest
        {
            ExternalOrderId = order.OrderCode,
            DeliveryAddress = BuildDeliveryAddress(order, request),
            Parcel = CheckoutCartBuilder.AggregateParcel(snapshots),
            GoodsValueAmount = order.Subtotal,
            CodAmount = codAmount
        }, cancellationToken);

        order.SetShippingFee(quoteResponse.TotalFeeAmount);
    }

    private static ShippingAddress BuildDeliveryAddress(Order order, CheckoutRequest request)
    {
        return BuildDeliveryAddress(
            request,
            fallbackStreet: order.ShippingAddress);
    }

    private static ShippingAddress BuildDeliveryAddress(CheckoutRequest request)
    {
        return BuildDeliveryAddress(
            request,
            fallbackStreet: request.ShippingAddress);
    }

    private static ShippingAddress BuildDeliveryAddress(CheckoutRequest request, string fallbackStreet)
    {
        return new ShippingAddress
        {
            Street = !string.IsNullOrWhiteSpace(request.ShippingStreet)
                ? request.ShippingStreet
                : fallbackStreet,
            Ward = request.ShippingWard,
            Province = request.ShippingProvince
        };
    }
}
