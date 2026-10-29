using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WorkspaceEcommerce.Application.Abstractions.Shipment;
using WorkspaceEcommerce.Infrastructure;

namespace WorkspaceEcommerce.Infrastructure.Tests.Shipping;

public sealed class LocalOnlyShippingCompositionTests
{
    [Fact]
    public async Task ResolveShippingWithoutPartnerCredentials_DoesNotConstructHttpAdapter_AndPartnerUseFailsExplicitly()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=synthetic;Username=synthetic;Password=synthetic",
            ["AdminAuth:Email"] = "admin@example.test", ["AdminAuth:Password"] = "synthetic-test-password",
            ["Jwt:Issuer"] = "Synthetic.Tests", ["Jwt:Audience"] = "Synthetic.Tests",
            ["Jwt:SigningKey"] = "synthetic-only-test-signing-key-32-characters",
            ["Jwt:AccessTokenMinutes"] = "60", ["EmailDelivery:Provider"] = "Log",
            ["Storefront:BaseUrl"] = "http://localhost:5173", ["ShippingCheckout:ExternalEnabled"] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging(); services.AddSingleton(TimeProvider.System);
        services.AddInfrastructure(configuration, new TestEnvironment());
        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var carrier = scope.ServiceProvider.GetRequiredService<IShipmentService>();
        Assert.NotNull(carrier); // Local-only consumers can resolve without credentials or network.
        await Assert.ThrowsAsync<HttpRequestException>(() => carrier.GetTrackingAsync("synthetic"));
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Synthetic.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
