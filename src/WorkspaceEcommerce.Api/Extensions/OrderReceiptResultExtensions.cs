using Microsoft.AspNetCore.Mvc;
using WorkspaceEcommerce.Application.Common.Models;
using WorkspaceEcommerce.Application.Modules.Ordering.Receipts;

namespace WorkspaceEcommerce.Api.Extensions;

internal static class OrderReceiptResultExtensions
{
    public static IActionResult ToOrderReceiptResponse(
        this ControllerBase controller,
        Result<OrderReceiptDocument> result)
    {
        if (!result.IsSuccess)
        {
            return controller.ToApiResponse(result);
        }

        controller.Response.Headers.CacheControl = "private, no-store, max-age=0";
        controller.Response.Headers.Pragma = "no-cache";
        controller.Response.Headers.XContentTypeOptions = "nosniff";

        var document = result.Value!;
        return controller.File(document.Content, document.ContentType, document.FileName);
    }
}
