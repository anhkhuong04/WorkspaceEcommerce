namespace WorkspaceEcommerce.Application.Common.Models;

public class Result
{
    protected Result(ResultStatus status, IReadOnlyCollection<string> errors)
    {
        Status = status;
        Errors = errors;
    }

    public ResultStatus Status { get; }

    public bool IsSuccess => Status == ResultStatus.Success;

    public bool IsFailure => !IsSuccess;

    public IReadOnlyCollection<string> Errors { get; }

    public string? FirstError => Errors.FirstOrDefault();

    public static Result Success()
    {
        return new Result(ResultStatus.Success, Array.Empty<string>());
    }

    public static Result Failure(string error)
    {
        return Failure([error]);
    }

    public static Result Failure(IEnumerable<string> errors)
    {
        return Create(ResultStatus.Failure, errors);
    }

    public static Result Validation(IEnumerable<string> errors)
    {
        return Create(ResultStatus.Validation, errors);
    }

    public static Result NotFound(string error)
    {
        return Create(ResultStatus.NotFound, [error]);
    }

    public static Result Conflict(string error)
    {
        return Create(ResultStatus.Conflict, [error]);
    }

    protected static Result Create(ResultStatus status, IEnumerable<string> errors)
    {
        var errorList = NormalizeErrors(errors);

        return new Result(status, errorList);
    }

    protected static string[] NormalizeErrors(IEnumerable<string> errors)
    {
        var errorList = errors
            .Where(error => !string.IsNullOrWhiteSpace(error))
            .Select(error => error.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return errorList.Length == 0
            ? ["An error occurred."]
            : errorList;
    }
}
