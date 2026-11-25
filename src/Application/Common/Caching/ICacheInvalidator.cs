namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Invalidates cached LDAP reads on this node and on every other node.
/// </summary>
public interface ICacheInvalidator
{
    /// <summary>
    /// Removes the given keys and tags from the local L1, the shared L2 and notifies the other nodes.
    /// </summary>
    Task InvalidateAsync(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default);
}
