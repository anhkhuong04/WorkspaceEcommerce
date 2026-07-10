using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using WorkspaceEcommerce.Application.Abstractions.Payments;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Infrastructure.Persistence;

namespace WorkspaceEcommerce.Api.IntegrationTests.Infrastructure;

public sealed class ApiIntegrationTestFixture : IAsyncLifetime
{
    private static readonly string[] EnvironmentVariableNames =
    [
        "ConnectionStrings__DefaultConnection",
        "AdminAuth__Email",
        "AdminAuth__Password",
        "Jwt__Issuer",
        "Jwt__Audience",
        "Jwt__SigningKey",
        "Jwt__AccessTokenMinutes",
        "Storefront__BaseUrl"
    ];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("workspace_ecommerce_tests")
        .WithUsername("workspace_ecommerce")
        .WithPassword("workspace_ecommerce_tests")
        .Build();
    private readonly Dictionary<string, string?> _previousEnvironmentVariables = [];

    private ApiTestWebApplicationFactory? _factory;

    public HttpClient CreateClient()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("The API test factory has not been initialized.");
        }

        return _factory.CreateClient();
    }

    public HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("The API test factory has not been initialized.");
        }

        return _factory.CreateClient(options);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
                payments.payment_transactions,
                loyalty.loyalty_transactions,
                loyalty.customer_loyalty_accounts,
                promotions.coupon_redemptions,
                promotions.coupon_product_targets,
                promotions.coupons,
                content.banners,
                content.blog_comments,
                content.blog_post_related_products,
                content.blog_posts,
                ordering.order_status_history,
                ordering.order_items,
                ordering.orders,
                cart.cart_items,
                cart.carts,
                customer.login_history,
                customer.customers,
                catalog.product_specifications,
                catalog.product_images,
                catalog.product_variants,
                catalog.products,
                catalog.categories
            RESTART IDENTITY CASCADE;
            """);
    }

    public async Task SeedAsync(Func<AppDbContext, Task> seed)
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await seed(dbContext);
        await dbContext.SaveChangesAsync();
    }

    public async Task<TResult> ExecuteDbAsync<TResult>(Func<AppDbContext, Task<TResult>> operation)
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await operation(dbContext);
    }

    public async Task<TResult> ExecuteScopeAsync<TResult>(Func<IServiceProvider, Task<TResult>> operation)
    {
        await using var scope = CreateScope();

        return await operation(scope.ServiceProvider);
    }

    private AsyncServiceScope CreateScope()
    {
        if (_factory is null)
        {
            throw new InvalidOperationException("The API test factory has not been initialized.");
        }

        return _factory.Services.CreateAsyncScope();
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        SetRuntimeConfiguration(_postgres.GetConnectionString());
        _factory = new ApiTestWebApplicationFactory();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _postgres.DisposeAsync();
        RestoreRuntimeConfiguration();
    }

    private void SetRuntimeConfiguration(string connectionString)
    {
        foreach (var name in EnvironmentVariableNames)
        {
            _previousEnvironmentVariables[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("AdminAuth__Email", "admin@example.com");
        Environment.SetEnvironmentVariable("AdminAuth__Password", "integration-test-password");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "WorkspaceEcommerce.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__Audience", "WorkspaceEcommerce.Admin.IntegrationTests");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-test-signing-key-32-bytes-minimum");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Storefront__BaseUrl", "http://localhost:5173");
    }

    private void RestoreRuntimeConfiguration()
    {
        foreach (var (name, value) in _previousEnvironmentVariables)
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private sealed class ApiTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IShipmentService>();
                services.AddScoped<IShipmentService, FakeIntegrationShipmentService>();
                services.RemoveAll<IVNPayPaymentService>();
                services.AddSingleton<IVNPayPaymentService, FakeIntegrationVNPayPaymentService>();
            });
        }
    }

    private sealed class FakeIntegrationVNPayPaymentService : IVNPayPaymentService
    {
        public string CreatePaymentUrl(VNPayCreatePaymentUrlRequest request)
        {
            return $"https://vnpay.integration.test/pay?vnp_TxnRef={Uri.EscapeDataString(request.TxnRef)}";
        }

        public VNPayCallbackVerificationResult VerifyCallback(
            IReadOnlyDictionary<string, string?> parameters)
        {
            parameters.TryGetValue("vnp_SecureHash", out var secureHash);

            return new VNPayCallbackVerificationResult(
                string.Equals(secureHash, "valid-hash", StringComparison.Ordinal),
                GetValue(parameters, "vnp_TxnRef"),
                TryParseGatewayAmount(GetValue(parameters, "vnp_Amount")),
                GetValue(parameters, "vnp_ResponseCode"),
                GetValue(parameters, "vnp_TransactionStatus"),
                GetValue(parameters, "vnp_TransactionNo"),
                secureHash,
                GetValue(parameters, "vnp_OrderInfo"),
                parameters);
        }

        public VNPayPaymentOutcome GetPaymentOutcome(string? responseCode, string? transactionStatus)
        {
            if (responseCode == "00" && (string.IsNullOrWhiteSpace(transactionStatus) || transactionStatus == "00"))
            {
                return VNPayPaymentOutcome.Success;
            }

            return responseCode == "24"
                ? VNPayPaymentOutcome.Cancelled
                : VNPayPaymentOutcome.Failed;
        }

        private static string? GetValue(IReadOnlyDictionary<string, string?> parameters, string key)
        {
            return parameters.TryGetValue(key, out var value)
                ? string.IsNullOrWhiteSpace(value) ? null : value.Trim()
                : null;
        }

        private static decimal? TryParseGatewayAmount(string? value)
        {
            return decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var amount)
                ? amount / 100m
                : null;
        }
    }

    private sealed class FakeIntegrationShipmentService : IShipmentService
    {
        public Task<ShippingQuoteResponse> GetShippingQuoteAsync(ShippingQuoteRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ShippingQuoteResponse
            {
                TotalFeeAmount = 0m,
                BaseFeeAmount = 0m,
                ExtraWeightFeeAmount = 0m,
                InsuranceFeeAmount = 0m,
                RouteType = "Mock",
                Currency = "VND"
            });
        }

        public Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CreateShipmentResponse
            {
                ShipmentId = Guid.NewGuid(),
                ExternalOrderId = request.ExternalOrderId,
                TrackingCode = "ML-MOCK-INT",
                Status = "PendingPickup",
                ShippingFeeAmount = 0m,
                Currency = "VND"
            });
        }

        public Task<TrackingResponse> GetTrackingAsync(string trackingCode, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TrackingResponse
            {
                TrackingCode = trackingCode,
                ExternalOrderId = "ECOM-1001",
                Status = "PendingPickup",
                ShippingFeeAmount = 0m,
                Timeline = []
            });
        }
    }
}
