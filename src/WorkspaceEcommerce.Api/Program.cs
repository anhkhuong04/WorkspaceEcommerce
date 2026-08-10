using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Authentication;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Configuration;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Api.Health;
using WorkspaceEcommerce.Api.Localization;
using WorkspaceEcommerce.Api.Hubs;
using WorkspaceEcommerce.Api.Middleware;
using WorkspaceEcommerce.Api.Observability;
using WorkspaceEcommerce.Application;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Seeding;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Infrastructure;
using WorkspaceEcommerce.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddEnvironmentVariables();
}

var jwtOptions = builder.Configuration.GetValidatedJwtOptions();
var dataProtectionKeyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
var runtimeLimits = builder.Configuration
    .GetSection(RuntimeLimitsOptions.SectionName)
    .Get<RuntimeLimitsOptions>() ?? new RuntimeLimitsOptions();
runtimeLimits.Validate();
ProductionRuntimeConfigurationValidator.Validate(builder.Configuration, builder.Environment);

builder.WebHost.ConfigureKestrel(runtimeLimits.ApplyTo);
builder.Services.Configure<HostOptions>(options =>
    options.ShutdownTimeout = TimeSpan.FromSeconds(runtimeLimits.ShutdownTimeoutSeconds));

var dataProtectionBuilder = builder.Services
    .AddDataProtection()
    .SetApplicationName("WorkspaceEcommerce");

if (!string.IsNullOrWhiteSpace(dataProtectionKeyRingPath))
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyRingPath));
}

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                    ? "The request is invalid."
                    : error.ErrorMessage);

            return new BadRequestObjectResult(
                ApiResponse<object>.Fail(errors, context.HttpContext.TraceIdentifier));
        };
    });
builder.Services.AddOpenApi();
builder.Services.AddApplicationAuthentication(jwtOptions);
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddApplicationInsightsTelemetryProcessor<SensitiveTelemetryRedactionProcessor>();
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCustomerContext, CurrentCustomerContext>();
builder.Services.AddScoped<ICurrentLanguageProvider, CurrentLanguageProvider>();
builder.Services.AddApplicationCors(builder.Configuration, builder.Environment);
builder.Services.AddApplicationForwardedHeaders(builder.Configuration);
builder.Services.AddApplicationRateLimiter(builder.Environment);
builder.Services
    .AddHealthChecks()
    .AddCheck<ApplicationLivenessHealthCheck>(
        "application-liveness",
        tags: ["live"])
    .AddCheck<DatabaseHealthCheck>(
        "postgresql",
        tags: ["ready"]);
builder.Services.AddSignalR();
builder.Services.AddScoped<WorkspaceEcommerce.Application.Abstractions.Notifications.INotificationService, WorkspaceEcommerce.Api.Hubs.SignalRNotificationService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.Any(argument => string.Equals(argument, "--seed-demo", StringComparison.OrdinalIgnoreCase)))
{
    await using var scope = app.Services.CreateAsyncScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDemoDataSeeder>();
    var result = await seeder.SeedAsync();
    Console.WriteLine(
        $"Demo data seed completed. Categories={result.Categories}, Products={result.Products}, Variants={result.Variants}, Banners={result.Banners}, Carts={result.Carts}, Orders={result.Orders}.");

    return;
}

app.UseForwardedHeaders();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (context.Context.Request.Path.StartsWithSegments("/media"))
        {
            context.Context.Response.ContentType = "image/webp";
            context.Context.Response.Headers["Content-Disposition"] = "inline";
            context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        }
    }
});
app.UseCors(CorsExtensions.FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});
app.MapControllers();
app.MapHub<NotificationHub>(NotificationHub.Route);

app.Run();

public partial class Program;
