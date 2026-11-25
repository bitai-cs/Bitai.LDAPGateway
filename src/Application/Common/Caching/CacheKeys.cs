using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bitai.LDAPGateway.Application.Directory.Queries.GetDirectoryEntryByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupParents;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserParents;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchDirectory;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchGroups;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchUsers;
using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Builds deterministic cache keys for the LDAP read operations.
/// </summary>
/// <remarks>
/// Keys never contain directory values: identifiers, filters and distinguished names are hashed,
/// so no personal data reaches Redis or the logs. The service/environment/version prefix is applied
/// by FusionCache through <c>CachingOptions.KeyPrefix</c> and is not repeated here.
/// </remarks>
public static class CacheKeys
{
    private const char Separator = '\u001F';

    /// <summary>Key for <see cref="SearchUsersQuery"/>.</summary>
    public static string SearchUsers(SearchUsersQuery query) =>
        Build("search-users", query.ServerProfile, query.CatalogType,
            ("fa", query.FilterAttribute),
            ("fv", Normalize(query.FilterValue)),
            ("sfa", query.SecondFilterAttribute),
            ("sfv", Normalize(query.SecondFilterValue)),
            ("cf", query.CombineFilters),
            ("set", query.RequiredAttributeSet));

    /// <summary>Key for <see cref="SearchGroupsQuery"/>.</summary>
    public static string SearchGroups(SearchGroupsQuery query) =>
        Build("search-groups", query.ServerProfile, query.CatalogType,
            ("fa", query.FilterAttribute),
            ("fv", Normalize(query.FilterValue)),
            ("sfa", query.SecondFilterAttribute),
            ("sfv", Normalize(query.SecondFilterValue)),
            ("cf", query.CombineFilters),
            ("set", query.RequiredAttributeSet));

    /// <summary>Key for <see cref="SearchDirectoryQuery"/>.</summary>
    public static string SearchDirectory(SearchDirectoryQuery query) =>
        Build("search-directory", query.ServerProfile, query.CatalogType,
            ("fa", query.FilterAttribute),
            ("fv", Normalize(query.FilterValue)),
            ("sfa", query.SecondFilterAttribute),
            ("sfv", Normalize(query.SecondFilterValue)),
            ("cf", query.CombineFilters),
            ("set", query.RequiredAttributeSet));

    /// <summary>Key for <see cref="GetUserByIdentifierQuery"/>.</summary>
    public static string User(GetUserByIdentifierQuery query) =>
        Build("user", query.ServerProfile, query.CatalogType,
            ("ida", query.IdentifierAttribute),
            ("id", Normalize(query.Identifier)),
            ("set", query.RequiredAttributeSet),
            ("must", query.UserMustExists));

    /// <summary>Key for <see cref="GetGroupByIdentifierQuery"/>.</summary>
    public static string Group(GetGroupByIdentifierQuery query) =>
        Build("group", query.ServerProfile, query.CatalogType,
            ("ida", query.IdentifierAttribute),
            ("id", Normalize(query.Identifier)),
            ("set", query.RequiredAttributeSet),
            ("must", query.GroupMustExists));

    /// <summary>Key for <see cref="GetDirectoryEntryByIdentifierQuery"/>.</summary>
    public static string Entry(GetDirectoryEntryByIdentifierQuery query) =>
        Build("entry", query.ServerProfile, query.CatalogType,
            ("ida", query.IdentifierAttribute),
            ("id", Normalize(query.Identifier)),
            ("set", query.RequiredAttributeSet),
            ("must", query.UserMustExists));

    /// <summary>Key for <see cref="GetUserParentsQuery"/>.</summary>
    public static string UserParents(GetUserParentsQuery query) =>
        Build("user-parents", query.ServerProfile, query.CatalogType,
            ("ida", query.IdentifierAttribute),
            ("id", Normalize(query.Identifier)),
            ("set", query.RequiredAttributeSet),
            ("must", query.UserMustExist));

    /// <summary>Key for <see cref="GetGroupParentsQuery"/>.</summary>
    public static string GroupParents(GetGroupParentsQuery query) =>
        Build("group-parents", query.ServerProfile, query.CatalogType,
            ("ida", query.IdentifierAttribute),
            ("id", Normalize(query.Identifier)),
            ("set", query.RequiredAttributeSet),
            ("must", query.GroupMustExist));

    /// <summary>Normalizes a directory value before it is hashed.</summary>
    /// <remarks>Active Directory matches these attributes case-insensitively and ignores outer whitespace.</remarks>
    public static string? Normalize(string? value) => value?.Trim().ToLowerInvariant();

    private static string Build(
        string operation,
        string serverProfile,
        CatalogType catalogType,
        params (string Name, object? Value)[] parts)
    {
        var builder = new StringBuilder(operation.Length + 64);
        Append(builder, operation);
        Append(builder, CacheTags.NormalizeProfile(serverProfile));
        Append(builder, (int)catalogType);

        foreach (var (name, value) in parts)
        {
            Append(builder, name);
            Append(builder, value);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
        return $"{operation}:{hash}";
    }

    private static void Append(StringBuilder builder, object? value)
    {
        var component = ToComponent(value);
        if (component is null)
        {
            builder.Append("-1:").Append(Separator);
            return;
        }

        builder.Append(component.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(component).Append(Separator);
    }

    private static string? ToComponent(object? value) => value switch
    {
        null => null,
        string text => text,
        bool flag => flag ? "1" : "0",
        Enum option => Convert.ToInt32(option, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
}

/// <summary>
/// Tags used to invalidate groups of cached reads.
/// </summary>
public static class CacheTags
{
    /// <summary>
    /// Tag shared by every cached read of a server profile (both catalogs).
    /// </summary>
    /// <remarks>
    /// Invalidation is intentionally coarse: a cached search result contains many entries and the
    /// gateway cannot know which searches include a changed entry, so a mutation invalidates all the
    /// cached reads of its profile.
    /// </remarks>
    public static string Profile(string serverProfile) => $"ldap:profile:{NormalizeProfile(serverProfile)}";

    /// <summary>Normalizes a profile id so every node builds the same tag.</summary>
    public static string NormalizeProfile(string serverProfile) => serverProfile.Trim().ToLowerInvariant();
}
