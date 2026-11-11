namespace DineDesk.Api.Services;

public enum ServiceErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
}

/// <summary>Outcome of a business operation, so controllers can map failures to the right HTTP status without services knowing about HTTP.</summary>
public class ServiceResult<T>
{
    private ServiceResult(bool succeeded, T? value, string? error, ServiceErrorType errorType)
    {
        Succeeded = succeeded;
        Value = value;
        Error = error;
        ErrorType = errorType;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public string? Error { get; }
    public ServiceErrorType ErrorType { get; }

    public static ServiceResult<T> Success(T value) => new(true, value, null, default);

    public static ServiceResult<T> Failure(string error, ServiceErrorType errorType = ServiceErrorType.Validation) =>
        new(false, default, error, errorType);
}
