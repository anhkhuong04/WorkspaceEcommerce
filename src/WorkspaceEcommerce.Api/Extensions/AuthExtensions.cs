using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Infrastructure.Configuration;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class AuthExtensions
{
    public static IServiceCollection AddApplicationAuthentication(
        this IServiceCollection services,
        JwtOptions jwtOptions)
    {
        services
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

        return services;
    }

    private static async Task WriteAuthErrorAsync(HttpContext context, int statusCode, string error)
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
}
