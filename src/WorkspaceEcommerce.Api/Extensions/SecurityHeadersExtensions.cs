namespace WorkspaceEcommerce.Api.Extensions;

internal static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Content-Security-Policy", "frame-ancestors 'none'");

            await next(context);
        });
    }
}
