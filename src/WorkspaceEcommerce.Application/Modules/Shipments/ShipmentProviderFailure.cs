using System.Net;

namespace WorkspaceEcommerce.Application.Modules.Shipments;

internal static class ShipmentProviderFailure
{
    public static bool IsTransient(HttpRequestException exception)
    {
        if (!exception.StatusCode.HasValue)
        {
            return true;
        }

        var statusCode = exception.StatusCode.Value;
        return statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)statusCode >= 500;
    }
}
