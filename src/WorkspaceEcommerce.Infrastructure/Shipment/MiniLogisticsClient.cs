using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Abstractions.Shipment;

namespace WorkspaceEcommerce.Infrastructure.Shipment;

internal sealed class MiniLogisticsClient(
    HttpClient httpClient,
    IOptions<MiniLogisticsOptions> options,
    ILogger<MiniLogisticsClient> logger) : IShipmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ShippingQuoteResponse> GetShippingQuoteAsync(
        ShippingQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            request.ExternalOrderId,
            PickupAddress = (object?)null,
            DeliveryAddress = new
            {
                Street = request.DeliveryAddress.Street,
                Ward = request.DeliveryAddress.Ward,
                Province = request.DeliveryAddress.Province,
                Country = request.DeliveryAddress.Country
            },
            Parcel = new
            {
                WeightKg = request.Parcel.WeightKg,
                LengthCm = request.Parcel.LengthCm,
                WidthCm = request.Parcel.WidthCm,
                HeightCm = request.Parcel.HeightCm
            },
            request.GoodsValueAmount,
            request.CodAmount,
            Currency = "VND"
        };

        logger.LogInformation("Requesting shipping quote for external order {ExternalOrderId}", request.ExternalOrderId);

        var response = await httpClient.PostAsJsonAsync(
            "shipping/quote",
            payload,
            JsonOptions,
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "shipping quote", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<ShippingQuoteResponse>(JsonOptions, cancellationToken);

        return result ?? throw new InvalidOperationException("MiniLogistics returned null shipping quote response.");
    }

    public async Task<CreateShipmentResponse> CreateShipmentAsync(
        CreateShipmentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            request.ExternalOrderId,
            Sender = (object?)null,
            Receiver = new
            {
                Name = request.Receiver.Name,
                Phone = request.Receiver.Phone
            },
            PickupAddress = (object?)null,
            DeliveryAddress = new
            {
                Street = request.DeliveryAddress.Street,
                Ward = request.DeliveryAddress.Ward,
                Province = request.DeliveryAddress.Province,
                Country = request.DeliveryAddress.Country
            },
            Parcel = new
            {
                WeightKg = request.Parcel.WeightKg,
                LengthCm = request.Parcel.LengthCm,
                WidthCm = request.Parcel.WidthCm,
                HeightCm = request.Parcel.HeightCm
            },
            request.GoodsValueAmount,
            request.CodAmount,
            Currency = "VND",
            request.Note
        };

        logger.LogInformation("Creating shipment for external order {ExternalOrderId}", request.ExternalOrderId);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "shipments");
        httpRequest.Content = JsonContent.Create(payload, options: JsonOptions);
        httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);

        var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "create shipment", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<CreateShipmentResponse>(JsonOptions, cancellationToken);

        return result ?? throw new InvalidOperationException("MiniLogistics returned null create shipment response.");
    }

    public async Task<TrackingResponse> GetTrackingAsync(
        string trackingCode,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching tracking for {TrackingCode}", trackingCode);

        var response = await httpClient.GetAsync(
            $"shipments/{Uri.EscapeDataString(trackingCode)}",
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "tracking", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TrackingResponse>(JsonOptions, cancellationToken);

        return result ?? throw new InvalidOperationException("MiniLogistics returned null tracking response.");
    }

    private async Task EnsureSuccessOrThrowAsync(
        HttpResponseMessage response,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "MiniLogistics {Operation} failed with status {StatusCode}: {ErrorBody}",
            operationName,
            (int)response.StatusCode,
            errorBody);

        throw new HttpRequestException(
            $"MiniLogistics {operationName} failed with status {(int)response.StatusCode}: {errorBody}",
            null,
            response.StatusCode);
    }
}
