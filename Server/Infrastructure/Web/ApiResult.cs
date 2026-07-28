namespace GM95.Server.Infrastructure.Web;

/// <summary>Bao response chuan: { ok, data, error }. Dong nhat cho moi API.</summary>
public sealed class ApiResult<T>
{
    public bool Ok { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }

    public static ApiResult<T> Success(T data) => new() { Ok = true, Data = data };
    public static ApiResult<T> Fail(string code, string message) =>
        new() { Ok = false, Error = new ApiError { Code = code, Message = message } };
}

public sealed class ApiError
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
}

/// <summary>Ket qua khong kem du lieu (chi ok/error).</summary>
public static class ApiResult
{
    public static ApiResult<object> Success() => ApiResult<object>.Success(new { });
}
