using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WorkspaceEcommerce.Api.Configuration;

namespace WorkspaceEcommerce.Api.IntegrationTests.Configuration;

public sealed class ProductionRuntimeConfigurationValidatorTests
{
    [Fact]
    public void Validate_ProductionWildcardAllowedHosts_Throws()
    {
        var configuration = BuildConfiguration(("AllowedHosts", "*"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionRuntimeConfigurationValidator.Validate(configuration, ProductionEnvironment()));

        Assert.Contains("AllowedHosts", exception.Message);
    }

    [Fact]
    public void Validate_ProductionRelativeDataProtectionPath_Throws()
    {
        var configuration = BuildConfiguration(("DataProtection:KeyRingPath", "keys"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionRuntimeConfigurationValidator.Validate(configuration, ProductionEnvironment()));

        Assert.Contains("DataProtection", exception.Message);
    }

    [Fact]
    public void Validate_ProductionExactHostsAndExternalSettings_Passes()
    {
        var configuration = BuildConfiguration(
            ("AllowedHosts", "api.example.test;media.example.test"),
            ("DataProtection:KeyRingPath", Path.GetFullPath(".tmp/test-keys")),
            ("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=00000000-0000-0000-0000-000000000001"),
            ("Storefront:BaseUrl", "https://shop.example.test"));

        ProductionRuntimeConfigurationValidator.Validate(configuration, ProductionEnvironment());
    }

    [Fact]
    public void Validate_DevelopmentWildcardAllowedHosts_IsAllowed()
    {
        var configuration = BuildConfiguration(("AllowedHosts", "*"));

        ProductionRuntimeConfigurationValidator.Validate(configuration, DevelopmentEnvironment());
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["AllowedHosts"] = "api.example.test",
            ["DataProtection:KeyRingPath"] = Path.GetFullPath(".tmp/test-keys"),
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000001",
            ["Storefront:BaseUrl"] = "https://shop.example.test"
        };

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment ProductionEnvironment() => new TestHostEnvironment(Environments.Production);

    private static IHostEnvironment DevelopmentEnvironment() => new TestHostEnvironment(Environments.Development);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "WorkspaceEcommerce.Api.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
