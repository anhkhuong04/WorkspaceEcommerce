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
    MiniLogisticsFailureGate failureGate,
    ILogger<MiniLogisticsClient> logger) : IShipmentService
{
    private readonly MiniLogisticsOptions clientOptions = options.Value;
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

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "shipping/quote")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            },
            "shipping quote",
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

        using var response = await SendWithRetryAsync(() =>
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "shipments")
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            httpRequest.Headers.Add("Idempotency-Key", idempotencyKey);
            return httpRequest;
        }, "create shipment", cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "create shipment", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<CreateShipmentResponse>(JsonOptions, cancellationToken);

        return result ?? throw new InvalidOperationException("MiniLogistics returned null create shipment response.");
    }

    public async Task<TrackingResponse> GetTrackingAsync(
        string trackingCode,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching tracking for {TrackingCode}", trackingCode);

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(
                HttpMethod.Get,
                $"shipments/{Uri.EscapeDataString(trackingCode)}"),
            "tracking",
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "tracking", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TrackingResponse>(JsonOptions, cancellationToken);

        return result ?? throw new InvalidOperationException("MiniLogistics returned null tracking response.");
    }

    public async Task<TrackingResponse> CancelShipmentAsync(
        string trackingCode,
        string reason,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Cancelling shipment {TrackingCode}", trackingCode);

        using var response = await SendWithRetryAsync(
            () => new HttpRequestMessage(
                HttpMethod.Post,
                $"shipments/{Uri.EscapeDataString(trackingCode)}/cancel")
            {
                Content = JsonContent.Create(new { Reason = reason }, options: JsonOptions)
            },
            "cancel shipment",
            cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "cancel shipment", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TrackingResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("MiniLogistics returned null cancel shipment response.");
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        string operationName,
        CancellationToken cancellationToken)
    {
        failureGate.ThrowIfOpen(operationName);
        var maxAttempts = Math.Max(1, clientOptions.MaxRetryAttempts + 1);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = requestFactory();
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, clientOptions.OperationTimeoutSeconds)));

            try
            {
                var response = await httpClient.SendAsync(request, timeoutSource.Token);
                if (response.IsSuccessStatusCode)
                {
                    failureGate.RecordSuccess();
                    return response;
                }

                if (!IsTransient(response.StatusCode))
                {
                    failureGate.RecordSuccess();
                    return response;
                }

                if (attempt == maxAttempts)
                {
                    failureGate.RecordTransientFailure();
                    return response;
                }

                var delay = GetRetryDelay(response, attempt);
                logger.LogWarning(
                    "MiniLogistics {Operation} returned transient status {StatusCode}; retry {Attempt}/{MaxAttempts} in {DelayMs} ms",
                    operationName,
                    (int)response.StatusCode,
                    attempt,
                    maxAttempts - 1,
                    delay.TotalMilliseconds);
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                var delay = GetRetryDelay(response: null, attempt);
                logger.LogWarning(
                    "MiniLogistics {Operation} timed out; retry {Attempt}/{MaxAttempts} in {DelayMs} ms",
                    operationName,
                    attempt,
                    maxAttempts - 1,
                    delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                failureGate.RecordTransientFailure();
                throw new HttpRequestException(
                    $"MiniLogistics {operationName} timed out after {clientOptions.OperationTimeoutSeconds} seconds.",
                    ex);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                var delay = GetRetryDelay(response: null, attempt);
                logger.LogWarning(
                    "MiniLogistics {Operation} failed transiently; retry {Attempt}/{MaxAttempts} in {DelayMs} ms",
                    operationName,
                    attempt,
                    maxAttempts - 1,
                    delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (HttpRequestException)
            {
                failureGate.RecordTransientFailure();
                throw;
            }
        }

        throw new HttpRequestException($"MiniLogistics {operationName} failed after all retry attempts.");
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage? response, int attempt)
    {
        var retryAfter = response?.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            return date > DateTimeOffset.UtcNow
                ? date - DateTimeOffset.UtcNow
                : TimeSpan.Zero;
        }

        var baseDelay = Math.Max(1, clientOptions.RetryBaseDelayMilliseconds);
        return TimeSpan.FromMilliseconds(baseDelay * Math.Pow(2, attempt - 1));
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode)
    {
        var numericStatus = (int)statusCode;
        return statusCode is System.Net.HttpStatusCode.RequestTimeout or System.Net.HttpStatusCode.TooManyRequests
            || numericStatus >= 500;
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

        logger.LogError(
            "MiniLogistics {Operation} failed with status {StatusCode}",
            operationName,
            (int)response.StatusCode);

        throw new HttpRequestException(
            $"MiniLogistics {operationName} failed with status {(int)response.StatusCode}.",
            null,
            response.StatusCode);
    }
}
