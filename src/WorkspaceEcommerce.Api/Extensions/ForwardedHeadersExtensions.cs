using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddApplicationForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var configuredProxies = configuration
            .GetSection("ForwardedHeaders:KnownProxies")
            .Get<string[]>() ?? [];
        var proxies = configuredProxies
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Select(value => IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException(
                    $"Configuration 'ForwardedHeaders:KnownProxies' contains invalid IP address '{value}'."))
            .ToArray();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }
        });

        return services;
    }
}
