using System.Threading.RateLimiting;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class RateLimiterExtensions
{
    public static IServiceCollection AddApplicationRateLimiter(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        services.AddRateLimiter(options =>
        {
            var isDevelopment = environment.IsDevelopment();
            var authPermitLimit = isDevelopment ? 1_000 : 10;
            var transactionPermitLimit = isDevelopment ? 2_000 : 60;
            var catalogPermitLimit = isDevelopment ? 5_000 : 240;
            var defaultPermitLimit = isDevelopment ? 3_000 : 120;

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var path = httpContext.Request.Path.Value ?? string.Empty;
                var partitionKey = GetRateLimitPartitionKey(httpContext);

                if (path.StartsWith("/api/customer/auth", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/admin/auth", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"auth:{partitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authPermitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

                if (path.StartsWith("/api/checkout", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/payments", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/webhooks", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"transaction:{partitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = transactionPermitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

                if (path.StartsWith("/api/products", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/categories", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/banners", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/api/blog-posts", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"catalog:{partitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = catalogPermitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"default:{partitionKey}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = defaultPermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromMinutes(1)
                    });
            });
        });

        return services;
    }

    private static string GetRateLimitPartitionKey(HttpContext httpContext)
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
