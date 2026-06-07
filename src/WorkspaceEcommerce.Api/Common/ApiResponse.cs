namespace WorkspaceEcommerce.Api.Common;

public sealed record ApiResponse<T>(
    bool Success,
    T? Data,
    IReadOnlyCollection<string> Errors,
    string TraceId)
{
    public static ApiResponse<T> Ok(T? data, string traceId)
    {
        return new ApiResponse<T>(true, data, Array.Empty<string>(), traceId);
    }

    public static ApiResponse<T> Fail(IEnumerable<string> errors, string traceId)
    {
        var normalizedErrors = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Select(error => error.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ApiResponse<T>(
            false,
            default,
            normalizedErrors.Length == 0 ? ["An error occurred."] : normalizedErrors,
            traceId);
    }
}
