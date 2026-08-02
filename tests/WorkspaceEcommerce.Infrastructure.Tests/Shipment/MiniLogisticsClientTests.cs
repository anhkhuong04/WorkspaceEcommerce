using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Infrastructure.Shipment;

namespace WorkspaceEcommerce.Infrastructure.Tests.Shipment;

public sealed class MiniLogisticsClientTests
{
    [Fact]
    public async Task CreateShipmentAsync_TransientFailure_RetriesWithFreshRequestAndStableIdempotencyKey()
    {
        var handler = new StubHttpMessageHandler((request, callCount) =>
        {
            Assert.Equal("ORDER-RETRY", request.Headers.GetValues("Idempotency-Key").Single());
            return callCount == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse(HttpStatusCode.OK, CreateResponseJson);
        });
        var client = CreateClient(handler, new MiniLogisticsOptions
        {
            MaxRetryAttempts = 1,
            RetryBaseDelayMilliseconds = 1,
            CircuitBreakerFailureThreshold = 5
        });

        var response = await client.CreateShipmentAsync(CreateRequest(), "ORDER-RETRY");

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("ML-CLIENT-1", response.TrackingCode);
    }

    [Fact]
    public async Task CancelShipmentAsync_Conflict_DoesNotRetry()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.Conflict));
        var client = CreateClient(handler, new MiniLogisticsOptions
        {
            MaxRetryAttempts = 3,
            RetryBaseDelayMilliseconds = 1
        });

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.CancelShipmentAsync("ML-CLIENT-1", "Already assigned"));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetTrackingAsync_RepeatedTransientFailure_OpensShortFailureGate()
    {
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var options = new MiniLogisticsOptions
        {
            MaxRetryAttempts = 0,
            CircuitBreakerFailureThreshold = 1,
            CircuitBreakerBreakSeconds = 30
        };
        var client = CreateClient(handler, options);

        var first = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTrackingAsync("ML-CLIENT-1"));
        var second = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetTrackingAsync("ML-CLIENT-1"));

        Assert.Equal(HttpStatusCode.BadGateway, first.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, second.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    private static MiniLogisticsClient CreateClient(
        HttpMessageHandler handler,
        MiniLogisticsOptions options)
    {
        var wrappedOptions = Options.Create(options);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://provider.test/api/v1/partner/"),
            Timeout = Timeout.InfiniteTimeSpan
        };
        var failureGate = new MiniLogisticsFailureGate(wrappedOptions, TimeProvider.System);
        return new MiniLogisticsClient(
            httpClient,
            wrappedOptions,
            failureGate,
            NullLogger<MiniLogisticsClient>.Instance);
    }

    private static CreateShipmentRequest CreateRequest()
    {
        return new CreateShipmentRequest
        {
            ExternalOrderId = "ORDER-RETRY",
            Receiver = new ShipmentContact { Name = "Customer", Phone = "0900000000" },
            DeliveryAddress = new ShippingAddress
            {
                Street = "9 Le Loi",
                Ward = "Ben Nghe",
                Province = "Ho Chi Minh City"
            },
            Parcel = new ShippingParcel
            {
                WeightKg = 1m,
                LengthCm = 20m,
                WidthCm = 15m,
                HeightCm = 10m
            },
            GoodsValueAmount = 100000m,
            CodAmount = 130000m
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private const string CreateResponseJson = """
        {
          "shipmentId": "11111111-1111-1111-1111-111111111111",
          "externalOrderId": "ORDER-RETRY",
          "trackingCode": "ML-CLIENT-1",
          "status": "PendingPickup",
          "shippingFeeAmount": 30000,
          "currency": "VND"
        }
        """;

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responseFactory(request, CallCount));
        }
    }
}
