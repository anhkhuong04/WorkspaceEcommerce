namespace WorkspaceEcommerce.Api.Extensions;

internal static class CorsExtensions
{
    public const string FrontendCorsPolicy = "FrontendCors";

    public static IServiceCollection AddApplicationCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(
                FrontendCorsPolicy,
                policy => policy
                    .WithOrigins(GetAllowedCorsOrigins(configuration, environment))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        return services;
    }

    private static string[] GetAllowedCorsOrigins(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        var localOrigins = environment.IsDevelopment()
            ? new[]
            {
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:5174",
                "http://127.0.0.1:5174"
            }
            : [];

        var origins = localOrigins
            .Concat(configuredOrigins)
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!environment.IsDevelopment() && origins.Length == 0)
        {
            throw new InvalidOperationException(
                "Configuration 'Cors:AllowedOrigins' must include at least one production frontend origin.");
        }

        return origins;
    }
}
