namespace WorkspaceEcommerce.Application.Common.Models;

public sealed class Result<T> : Result
{
    private Result(T? value, ResultStatus status, IReadOnlyCollection<string> errors)
        : base(status, errors)
    {
        Value = value;
    }

    public T? Value { get; }

    public static Result<T> Success(T value)
    {
        return new Result<T>(value, ResultStatus.Success, Array.Empty<string>());
    }

    public static new Result<T> Failure(string error)
    {
        return Failure([error]);
    }

    public static new Result<T> Failure(IEnumerable<string> errors)
    {
        return CreateTyped(ResultStatus.Failure, errors);
    }

    public static new Result<T> Validation(IEnumerable<string> errors)
    {
        return CreateTyped(ResultStatus.Validation, errors);
    }

    public static new Result<T> NotFound(string error)
    {
        return CreateTyped(ResultStatus.NotFound, [error]);
    }

    public static new Result<T> Conflict(string error)
    {
        return CreateTyped(ResultStatus.Conflict, [error]);
    }

    public static new Result<T> Unauthorized(string error)
    {
        return CreateTyped(ResultStatus.Unauthorized, [error]);
    }

    private static Result<T> CreateTyped(ResultStatus status, IEnumerable<string> errors)
    {
        var errorList = NormalizeErrors(errors);

        return new Result<T>(default, status, errorList);
    }
}
