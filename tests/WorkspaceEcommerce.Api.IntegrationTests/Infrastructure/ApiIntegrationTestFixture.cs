using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
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
        "Jwt__AccessTokenMinutes"
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

    public async Task ResetDatabaseAsync()
    {
        await using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE
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
            });
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
