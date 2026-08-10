using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddApplicationForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ForwardedHeadersOptions>();

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
            .Distinct()
            .ToArray();

        // Do not opt into header processing merely because this middleware is
        // present in the pipeline. With no explicitly trusted proxy, the
        // default options process no forwarded headers at all.
        if (proxies.Length == 0)
        {
            return services;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            // ASP.NET Core otherwise trusts its default loopback network in
            // addition to configured values. Trust exactly the ingress IPs
            // supplied by deployment configuration instead.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();

            foreach (var proxy in proxies)
            {
                options.KnownProxies.Add(proxy);
            }
        });

        return services;
    }
}
