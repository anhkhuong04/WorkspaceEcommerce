using System.Net.Mime;
using System.Text.Json;
using WorkspaceEcommerce.Api.Common;

namespace WorkspaceEcommerce.Api.Middleware;

internal sealed class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled exception while processing request {TraceId}.", context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var response = ApiResponse<object>.Fail(
                ["An unexpected error occurred."],
                context.TraceIdentifier);

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                response,
                JsonSerializerOptions.Web,
                context.RequestAborted);
        }
    }
}
