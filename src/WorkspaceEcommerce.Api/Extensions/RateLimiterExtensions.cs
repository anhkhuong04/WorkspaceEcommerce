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
            var commentPermitLimit = isDevelopment ? 500 : 3;
            var twoFactorVerificationPermitLimit = isDevelopment ? 500 : 5;
            var twoFactorSetupPermitLimit = isDevelopment ? 500 : 5;
            var transactionPermitLimit = isDevelopment ? 2_000 : 60;
            var catalogPermitLimit = isDevelopment ? 5_000 : 240;
            var defaultPermitLimit = isDevelopment ? 3_000 : 120;

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var path = httpContext.Request.Path.Value ?? string.Empty;
                var partitionKey = GetRateLimitPartitionKey(httpContext);

                if (path.StartsWith("/api/blog-posts/", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith("/comments", StringComparison.OrdinalIgnoreCase) &&
                    HttpMethods.IsPost(httpContext.Request.Method))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"blog-comment:{partitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = commentPermitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

                if (path.StartsWith("/api/customer/auth/2fa", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"two-factor-verification:{partitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = twoFactorVerificationPermitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

                if (path.StartsWith("/api/customer/me/2fa", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"two-factor-setup:{partitionKey}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = twoFactorSetupPermitLimit,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

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
        // Use RemoteIpAddress only. UseForwardedHeaders may replace it, but only after the
        // request passes through a configured trusted proxy; raw headers are attacker-controlled.
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
