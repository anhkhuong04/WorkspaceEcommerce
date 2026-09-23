using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using WorkspaceEcommerce.Api.Configuration;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class RateLimiterExtensions
{
    public static IServiceCollection AddApplicationRateLimiter(
        this IServiceCollection services,
        RateLimitingOptions settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        var window = TimeSpan.FromSeconds(settings.WindowSeconds);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var leaseRetryAfter)
                    ? leaseRetryAfter
                    : window;
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(
                        1,
                        (int)Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var path = httpContext.Request.Path;
                var clientKey = GetClientPartitionKey(httpContext);

                if (path.StartsWithSegments("/api/orders/lookup/receipt"))
                {
                    return CreateFixedWindow(
                        $"guest-receipt:{clientKey}",
                        settings.GuestReceiptPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/blog-posts") &&
                    path.Value?.EndsWith("/comments", StringComparison.OrdinalIgnoreCase) == true &&
                    HttpMethods.IsPost(httpContext.Request.Method))
                {
                    return CreateFixedWindow(
                        $"blog-comment:{clientKey}",
                        settings.BlogCommentPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/customer/auth/2fa"))
                {
                    return CreateFixedWindow(
                        $"two-factor-verification:{clientKey}",
                        settings.TwoFactorVerificationPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/customer/me/2fa"))
                {
                    return CreateFixedWindow(
                        $"two-factor-setup:{GetIdentityPartitionKey(httpContext)}",
                        settings.TwoFactorSetupPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/customer/auth") ||
                    path.StartsWithSegments("/api/admin/auth"))
                {
                    return CreateFixedWindow(
                        $"auth:{clientKey}",
                        settings.AuthPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/warranties/lookup"))
                {
                    return CreateFixedWindow(
                        $"warranty-lookup:{clientKey}",
                        settings.WarrantyLookupPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/customer/warranties/activate"))
                {
                    return CreateFixedWindow(
                        $"warranty-activation:{GetIdentityPartitionKey(httpContext)}",
                        settings.WarrantyActivationPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/webhooks"))
                {
                    return CreateFixedWindow(
                        $"provider-webhook:{clientKey}",
                        settings.ProviderWebhookPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/checkout"))
                {
                    return CreateFixedWindow(
                        $"checkout:{GetIdentityPartitionKey(httpContext)}",
                        settings.CheckoutPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/payments"))
                {
                    return CreateFixedWindow(
                        $"payment:{GetIdentityPartitionKey(httpContext)}",
                        settings.PaymentPermitLimit,
                        window);
                }

                if (path.StartsWithSegments("/api/products") ||
                    path.StartsWithSegments("/api/categories") ||
                    path.StartsWithSegments("/api/banners") ||
                    path.StartsWithSegments("/api/blog-posts"))
                {
                    return CreateFixedWindow(
                        $"catalog:{clientKey}",
                        settings.CatalogPermitLimit,
                        window);
                }

                return CreateFixedWindow(
                    $"default:{GetIdentityPartitionKey(httpContext)}",
                    settings.DefaultPermitLimit,
                    window);
            });
        });

        return services;
    }

    internal static string GetIdentityPartitionKey(HttpContext httpContext)
    {
        var clientKey = GetClientPartitionKey(httpContext);
        var customerId = GetCustomerId(httpContext.User);

        return customerId.HasValue
            ? $"customer:{customerId.Value:N}:{clientKey}"
            : $"anonymous:{clientKey}";
    }

    internal static string GetClientPartitionKey(HttpContext httpContext)
    {
        // ForwardedHeaders may replace RemoteIpAddress only for explicitly trusted
        // proxies. Never partition directly on attacker-controlled forwarding headers.
        var address = httpContext.Connection.RemoteIpAddress;
        if (address is not null)
        {
            var normalized = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
            return $"ip:{normalized}";
        }

        return $"connection:{httpContext.Connection.Id}";
    }

    private static RateLimitPartition<string> CreateFixedWindow(
        string partitionKey,
        int permitLimit,
        TimeSpan window)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = window,
                AutoReplenishment = true
            });
    }

    private static Guid? GetCustomerId(ClaimsPrincipal user)
    {
        var value = user.FindFirst("customer_id")?.Value ??
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var customerId) && customerId != Guid.Empty
            ? customerId
            : null;
    }
}
