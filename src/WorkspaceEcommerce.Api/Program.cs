using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Api.Middleware;
using WorkspaceEcommerce.Application;
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
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                await WriteAuthErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Authentication is required.");
            },
            OnForbidden = async context =>
            {
                await WriteAuthErrorAsync(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Access is forbidden.");
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task WriteAuthErrorAsync(HttpContext context, int statusCode, string error)
{
    if (context.Response.HasStarted)
    {
        return;
    }

    context.Response.StatusCode = statusCode;
    context.Response.ContentType = MediaTypeNames.Application.Json;

    var response = ApiResponse<object>.Fail([error], context.TraceIdentifier);
    await JsonSerializer.SerializeAsync(
        context.Response.Body,
        response,
        JsonSerializerOptions.Web,
        context.RequestAborted);
}

public partial class Program;
