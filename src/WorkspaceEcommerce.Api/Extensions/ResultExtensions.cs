using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Api.Common;
using WorkspaceEcommerce.Application.Common.Models;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class ResultExtensions
{
    public static IActionResult ToApiResponse<T>(
        this ControllerBase controller,
        Result<T> result,
        int successStatusCode = StatusCodes.Status200OK)
    {
        var traceId = controller.HttpContext.TraceIdentifier;

        if (result.IsSuccess)
        {
            return controller.StatusCode(
                successStatusCode,
                ApiResponse<T>.Ok(result.Value, traceId));
        }

        var statusCode = result.Status switch
        {
            ResultStatus.Validation => StatusCodes.Status400BadRequest,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return controller.StatusCode(
            statusCode,
            ApiResponse<object>.Fail(result.Errors, traceId));
    }
}
