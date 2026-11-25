using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Signals FusionCache that the factory failed for an infrastructure reason, so the value must not be cached.
/// </summary>
internal sealed class CacheBypassException : Exception
{
    public CacheBypassException(Error error)
        : base(error.Message)
    {
        Error = error;
    }

    public Error Error { get; }
}
