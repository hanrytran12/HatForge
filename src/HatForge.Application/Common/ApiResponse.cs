namespace HatForge.Application.Common;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    private ApiResponse() { }

    public static ApiResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    public static ApiResponse<T> Fail(string error) => new()
    {
        Success = false,
        Error = error
    };

    public static ApiResponse<T> FailValidation(string error, IReadOnlyDictionary<string, string[]> errors) => new()
    {
        Success = false,
        Error = error,
        Errors = errors
    };
}

public static class ApiResponse
{
    public static ApiResponse<object> Ok() => ApiResponse<object>.Ok(new { });
    public static ApiResponse<T> Ok<T>(T data) => ApiResponse<T>.Ok(data);
    public static ApiResponse<object> Fail(string error) => ApiResponse<object>.Fail(error);
}
