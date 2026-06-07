using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Middleware;
using WorkspaceEcommerce.Application;
using WorkspaceEcommerce.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();

app.Run();

public partial class Program;
