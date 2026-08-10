using Microsoft.Extensions.Configuration;
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

        var options = configuration.GetValidatedEmailDeliveryOptions("Development");

        Assert.Equal(12, options.WorkerBatchSize);
        Assert.Equal(300, options.LeaseDurationSeconds);
        Assert.Equal(5, options.MaxDeliveryAttempts);
    }

    [Theory]
    [InlineData("EmailDelivery:WorkerBatchSize", "0")]
    [InlineData("EmailDelivery:LeaseDurationSeconds", "14")]
    [InlineData("EmailDelivery:MaxDeliveryAttempts", "21")]
    public void GetValidatedEmailDeliveryOptions_RejectsUnsafeLeaseSettings(string key, string value)
    {
        var configuration = BuildConfiguration((key, value));

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedEmailDeliveryOptions("Development"));
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
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
}
