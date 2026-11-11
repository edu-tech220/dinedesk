namespace DineDesk.Web.ApiClient;

public class ApiResult<T>
{
    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public string? Error { get; private init; }

    public static ApiResult<T> Success(T value) => new() { Succeeded = true, Value = value };

    public static ApiResult<T> Failure(string error) => new() { Succeeded = false, Error = error };
}
