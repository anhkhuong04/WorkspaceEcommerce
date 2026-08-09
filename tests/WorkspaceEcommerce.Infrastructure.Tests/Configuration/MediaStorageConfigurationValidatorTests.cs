using Microsoft.Extensions.Configuration;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Infrastructure.Tests.Configuration;

public sealed class MediaStorageConfigurationValidatorTests
{
    [Fact]
    public void GetValidatedMediaStorageOptions_LocalInProduction_Throws()
    {
        var configuration = BuildConfiguration(("MediaStorage:Provider", "Local"));

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedMediaStorageOptions("Production"));
    }

    [Fact]
    public void GetValidatedMediaStorageOptions_S3WithoutSecret_Throws()
    {
        var configuration = BuildConfiguration(
            ("MediaStorage:Provider", "S3"),
            ("MediaStorage:Bucket", "assets"),
            ("MediaStorage:ServiceUrl", "http://minio:9000"),
            ("MediaStorage:Region", "us-east-1"),
            ("MediaStorage:AccessKey", "CHANGE_ME"),
            ("MediaStorage:SecretKey", "CHANGE_ME"));

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetValidatedMediaStorageOptions("Production"));
    }

    [Fact]
    public void GetValidatedMediaStorageOptions_DevelopmentLocal_ReturnsConfiguredOptions()
    {
        var configuration = BuildConfiguration(("MediaStorage:Provider", "Local"));

        var options = configuration.GetValidatedMediaStorageOptions("Development");

        Assert.Equal("Local", options.Provider);
    }

    [Fact]
    public void GetValidatedMediaStorageOptions_ProductionS3WithHttpsCdn_ReturnsOptions()
    {
        var configuration = BuildConfiguration(
            ("MediaStorage:Provider", "S3"),
            ("MediaStorage:Bucket", "assets"),
            ("MediaStorage:ServiceUrl", "https://s3.example.test"),
            ("MediaStorage:Region", "us-east-1"),
            ("MediaStorage:AccessKey", "integration-access-key"),
            ("MediaStorage:SecretKey", "integration-secret-key"));

        var options = configuration.GetValidatedMediaStorageOptions("Production");

        Assert.Equal("S3", options.Provider);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        var baseline = new Dictionary<string, string?>
        {
            ["MediaStorage:PublicBaseUrl"] = "https://assets.example.test",
            ["MediaStorage:MaxUploadBytes"] = "5242880",
            ["MediaStorage:MaxWidth"] = "4096",
            ["MediaStorage:MaxHeight"] = "4096",
            ["MediaStorage:MaxPixels"] = "16000000",
            ["MediaStorage:CleanupRetentionHours"] = "24"
        };
        foreach (var (key, value) in values)
        {
            baseline[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(baseline).Build();
    }
}
