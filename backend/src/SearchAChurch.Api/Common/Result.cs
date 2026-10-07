namespace SearchAChurch.Api.Common;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public IDictionary<string, string[]>? ValidationErrors { get; }

    protected Result(bool isSuccess, string? errorCode, string? errorMessage, IDictionary<string, string[]>? validationErrors = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ValidationErrors = validationErrors;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);
    public static Result ValidationFailure(IDictionary<string, string[]> validationErrors) =>
        new(false, "VALIDATION_FAILED", "Um ou mais erros de validação ocorreram.", validationErrors);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage, IDictionary<string, string[]>? validationErrors = null)
        : base(isSuccess, errorCode, errorMessage, validationErrors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public new static Result<T> Failure(string errorCode, string errorMessage) => new(false, default, errorCode, errorMessage);
    public new static Result<T> ValidationFailure(IDictionary<string, string[]> validationErrors) =>
        new(false, default, "VALIDATION_FAILED", "Um ou mais erros de validação ocorreram.", validationErrors);
}
