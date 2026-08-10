using WorkspaceEcommerce.Api.Configuration;

namespace WorkspaceEcommerce.Api.IntegrationTests.Configuration;

public sealed class RuntimeLimitsOptionsTests
{
    [Fact]
    public void Validate_DefaultsPass()
    {
        var options = new RuntimeLimitsOptions();

        options.Validate();
    }

    [Fact]
    public void Validate_RejectsUnboundedRequestBody()
    {
        var options = new RuntimeLimitsOptions
        {
            MaxRequestBodyBytes = 65 * 1024 * 1024
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("MaxRequestBodyBytes", exception.Message);
    }

    [Fact]
    public void Validate_RejectsTooShortShutdownWindow()
    {
        var options = new RuntimeLimitsOptions
        {
            ShutdownTimeoutSeconds = 1
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("ShutdownTimeoutSeconds", exception.Message);
    }
}
