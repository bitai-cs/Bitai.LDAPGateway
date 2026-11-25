using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Serializable envelope for a cached <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// <see cref="Result{T}"/> cannot be serialized because its constructor is not public, so the cache
/// stores this envelope and rebuilds the result on every read. Only successful values and
/// not-found results are stored; infrastructure failures never reach the cache.
/// </remarks>
public sealed record CachedResult<T>(bool IsSuccess, T? Value, string? ErrorCode, string? ErrorMessage, int StatusCode)
{
    /// <summary>Creates an envelope for a successful operation.</summary>
    public static CachedResult<T> Success(T? value) => new(true, value, null, null, 200);

    /// <summary>Creates an envelope for a not-found result.</summary>
    public static CachedResult<T> NotFound(Error error) => Failure(error);

    /// <summary>Creates an envelope that mirrors a failed operation.</summary>
    public static CachedResult<T> Failure(Error error) => new(false, default, error.Code, error.Message, error.StatusCode);

    /// <summary>Rebuilds the application result.</summary>
    public Result<T> ToResult() =>
        IsSuccess
            ? Result<T>.Success(Value!)
            : Result<T>.Failure(new Error(
                ErrorCode ?? "error",
                ErrorMessage ?? "Unexpected error.",
                StatusCode));
}

/// <summary>Result of a cached read plus whether it was served from the cache.</summary>
public readonly record struct CachedQueryResult<T>(Result<T> Result, bool ServedFromCache);
