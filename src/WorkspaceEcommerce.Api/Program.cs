using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Authentication;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Extensions;
using WorkspaceEcommerce.Api.Health;
using WorkspaceEcommerce.Api.Localization;
using WorkspaceEcommerce.Api.Hubs;
using WorkspaceEcommerce.Api.Middleware;
using WorkspaceEcommerce.Application;
using WorkspaceEcommerce.Application.Abstractions.Authentication;
using WorkspaceEcommerce.Application.Abstractions.Seeding;
using WorkspaceEcommerce.Application.Common.Localization;
using WorkspaceEcommerce.Infrastructure;
using WorkspaceEcommerce.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);
var jwtOptions = builder.Configuration.GetValidatedJwtOptions();

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
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentCustomerContext, CurrentCustomerContext>();
builder.Services.AddScoped<ICurrentLanguageProvider, CurrentLanguageProvider>();
builder.Services.AddApplicationCors(builder.Configuration, builder.Environment);
builder.Services.AddApplicationRateLimiter(builder.Environment);
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "postgresql",
        tags: ["ready"]);
builder.Services.AddSignalR();
builder.Services.AddScoped<WorkspaceEcommerce.Application.Abstractions.Notifications.INotificationService, WorkspaceEcommerce.Api.Hubs.SignalRNotificationService>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseCors(CorsExtensions.FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});
app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

public partial class Program;
