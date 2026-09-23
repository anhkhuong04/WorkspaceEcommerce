using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Tests.Configuration;

public sealed class EmailDeliveryConfigurationValidatorTests
{
    [Fact]
    public void GetValidatedEmailDeliveryOptions_ReturnsBoundedLeaseSettings()
    {
        var configuration = BuildConfiguration(
            ("EmailDelivery:WorkerBatchSize", "12"),
            ("EmailDelivery:LeaseDurationSeconds", "300"),
            ("EmailDelivery:MaxDeliveryAttempts", "5"));

        var options = configuration.GetValidatedEmailDeliveryOptions(Environment(Environments.Development));

        Assert.Equal(12, options.WorkerBatchSize);
        Assert.Equal(300, options.LeaseDurationSeconds);
        Assert.Equal(5, options.MaxDeliveryAttempts);
    }

    [Theory]
    [InlineData("Development", false)]
    [InlineData("Staging", true)]
    [InlineData("QA", true)]
    [InlineData("Production", true)]
    public void GetValidatedEmailDeliveryOptions_LogProvider_IsDevelopmentOnly(
        string environmentName,
        bool shouldThrow)
    {
        var configuration = BuildConfiguration();

        var action = () => configuration.GetValidatedEmailDeliveryOptions(Environment(environmentName));

        if (shouldThrow)
        {
            var exception = Assert.Throws<InvalidOperationException>(action);
            Assert.Contains("EmailDelivery:Provider", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal("Log", action().Provider);
        }
    }

    [Theory]
    [InlineData("Staging")]
    [InlineData("QA")]
    [InlineData("Production")]
    public void GetValidatedEmailDeliveryOptions_SmtpWithoutTlsOutsideDevelopment_Throws(
        string environmentName)
    {
        var configuration = BuildSmtpConfiguration(("EmailDelivery:EnableSsl", "false"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedEmailDeliveryOptions(Environment(environmentName)));

        Assert.Contains("EmailDelivery:EnableSsl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetValidatedEmailDeliveryOptions_DevelopmentSmtpWithoutTls_IsAllowed()
    {
        var configuration = BuildSmtpConfiguration(("EmailDelivery:EnableSsl", "false"));

        var options = configuration.GetValidatedEmailDeliveryOptions(Environment(Environments.Development));

        Assert.Equal("Smtp", options.Provider);
        Assert.False(options.EnableSsl);
    }

    [Theory]
    [InlineData("smtp-user", null)]
    [InlineData(null, "smtp-password")]
    public void GetValidatedEmailDeliveryOptions_PartialSmtpCredentials_Throws(
        string? userName,
        string? password)
    {
        var configuration = BuildSmtpConfiguration(
            ("EmailDelivery:UserName", userName),
            ("EmailDelivery:Password", password));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedEmailDeliveryOptions(Environment(Environments.Production)));

        Assert.Contains("EmailDelivery:UserName", exception.Message, StringComparison.Ordinal);
        Assert.Contains("EmailDelivery:Password", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("smtp-password", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CHANGE_ME", "smtp-password")]
    [InlineData("smtp-user", "YOUR_SMTP_PASSWORD")]
    public void GetValidatedEmailDeliveryOptions_PlaceholderSmtpCredentials_Throws(
        string userName,
        string password)
    {
        var configuration = BuildSmtpConfiguration(
            ("EmailDelivery:UserName", userName),
            ("EmailDelivery:Password", password));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedEmailDeliveryOptions(Environment(Environments.Production)));

        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("QA")]
    [InlineData("Production")]
    public void GetValidatedEmailDeliveryOptions_ValidSmtp_PassesEnvironmentMatrix(string environmentName)
    {
        var configuration = BuildSmtpConfiguration(
            ("EmailDelivery:UserName", "smtp-user"),
            ("EmailDelivery:Password", "smtp-password"));

        var options = configuration.GetValidatedEmailDeliveryOptions(Environment(environmentName));

        Assert.Equal("Smtp", options.Provider);
        Assert.True(options.EnableSsl);
        Assert.Equal("smtp-user", options.UserName);
    }

    [Theory]
    [InlineData("EmailDelivery:WorkerBatchSize", "0")]
    [InlineData("EmailDelivery:LeaseDurationSeconds", "14")]
    [InlineData("EmailDelivery:MaxDeliveryAttempts", "21")]
    public void GetValidatedEmailDeliveryOptions_RejectsUnsafeLeaseSettings(string key, string value)
    {
        var configuration = BuildConfiguration((key, value));

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedEmailDeliveryOptions(Environment(Environments.Development)));
    }

    [Fact]
    public void AddInfrastructure_StagingLogProvider_FailsDuringComposition()
    {
        var configuration = BuildConfiguration(
            ("ConnectionStrings:DefaultConnection", "Host=localhost;Database=workspace;Username=test;Password=test"),
            ("AdminAuth:Email", "admin@example.com"),
            ("AdminAuth:Password", "integration-admin-password"),
            ("Jwt:Issuer", "WorkspaceEcommerce.Tests"),
            ("Jwt:Audience", "WorkspaceEcommerce.Tests"),
            ("Jwt:SigningKey", "integration-signing-key-at-least-32-bytes"),
            ("Jwt:AccessTokenMinutes", "60"),
            ("Storefront:BaseUrl", "https://shop.example.test"));
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, Environment(Environments.Staging)));

        Assert.Contains("EmailDelivery:Provider", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string? Value)[] values)
    {
        var baseline = new Dictionary<string, string?>
        {
            ["EmailDelivery:Provider"] = "Log",
            ["EmailDelivery:WorkerIntervalSeconds"] = "30",
            ["EmailDelivery:WorkerBatchSize"] = "20",
            ["EmailDelivery:LeaseDurationSeconds"] = "120",
            ["EmailDelivery:MaxDeliveryAttempts"] = "8"
        };
        foreach (var (key, value) in values)
        {
            baseline[key] = value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(baseline)
            .Build();
    }

    private static IConfiguration BuildSmtpConfiguration(params (string Key, string? Value)[] values)
    {
        var smtp = new List<(string Key, string? Value)>
        {
            ("EmailDelivery:Provider", "Smtp"),
            ("EmailDelivery:Host", "smtp.example.test"),
            ("EmailDelivery:Port", "587"),
            ("EmailDelivery:EnableSsl", "true"),
            ("EmailDelivery:SenderEmail", "no-reply@example.test")
        };
        smtp.AddRange(values);
        return BuildConfiguration([.. smtp]);
    }

    private static IHostEnvironment Environment(string environmentName) =>
        new TestHostEnvironment(environmentName);

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "WorkspaceEcommerce.Infrastructure.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
