using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WorkspaceEcommerce.Api.Extensions;

namespace WorkspaceEcommerce.Api.IntegrationTests.Configuration;

public sealed class ForwardedHeadersExtensionsTests
{
    [Fact]
    public void AddApplicationForwardedHeaders_WithoutConfiguredProxy_DoesNotEnableForwarding()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration();

        services.AddApplicationForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(ForwardedHeaders.None, options.ForwardedHeaders);
    }

    [Fact]
    public void AddApplicationForwardedHeaders_WithConfiguredProxy_TrustsOnlyThatProxy()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("ForwardedHeaders:KnownProxies:0", "10.10.0.4"));

        services.AddApplicationForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        Assert.Equal(
            ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            options.ForwardedHeaders);
        Assert.True(options.RequireHeaderSymmetry);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Empty(options.KnownIPNetworks);
        var proxy = Assert.Single(options.KnownProxies);
        Assert.Equal("10.10.0.4", proxy.ToString());
    }

    [Fact]
    public void AddApplicationForwardedHeaders_WithInvalidProxy_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(("ForwardedHeaders:KnownProxies:0", "not-an-address"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddApplicationForwardedHeaders(configuration));

        Assert.Contains("KnownProxies", exception.Message);
    }

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(value => value.Key, value => (string?)value.Value))
            .Build();
    }
}
