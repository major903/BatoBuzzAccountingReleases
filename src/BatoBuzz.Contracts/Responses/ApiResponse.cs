namespace BatoBuzz.Contracts.Responses;

/// <summary>
/// Generic API response wrapper.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<T> Fail(params string[] errors) =>
        new() { Success = false, Errors = errors.ToList() };
}

public class ApiResponse
{
    public bool Success { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? Message { get; set; }

    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse Fail(params string[] errors) =>
        new() { Success = false, Errors = errors.ToList() };
}
