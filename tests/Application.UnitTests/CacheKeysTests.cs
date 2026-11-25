using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchUsers;
using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class CacheKeysTests
{
    private const string Profile = "EDU";
    private const CatalogType Catalog = CatalogType.GC;

    [Fact]
    public void SameQuery_ProducesSameKey()
    {
        var first = CacheKeys.SearchUsers(Query("jdoe"));
        var second = CacheKeys.SearchUsers(Query("jdoe"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void CaseAndWhitespaceDifferences_ProduceSameKey()
    {
        var first = CacheKeys.SearchUsers(Query("JDoe"));
        var second = CacheKeys.SearchUsers(Query("  jdoe  "));

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentFilterValue_ProducesDifferentKey()
    {
        Assert.NotEqual(CacheKeys.SearchUsers(Query("jdoe")), CacheKeys.SearchUsers(Query("asmith")));
    }

    [Fact]
    public void DifferentAttributeSet_ProducesDifferentKey()
    {
        var first = CacheKeys.SearchUsers(Query("jdoe", LdapEntryAttributeSet.Minimum));
        var second = CacheKeys.SearchUsers(Query("jdoe", LdapEntryAttributeSet.All));

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DifferentCatalog_ProducesDifferentKey()
    {
        var search = new SearchUsersQuery(Profile, Catalog, LdapEntryAttribute.cn, "jdoe", null, null, null, LdapEntryAttributeSet.Minimum);
        var globalCatalog = new SearchUsersQuery(Profile, CatalogType.LC, LdapEntryAttribute.cn, "jdoe", null, null, null, LdapEntryAttributeSet.Minimum);

        Assert.NotEqual(CacheKeys.SearchUsers(search), CacheKeys.SearchUsers(globalCatalog));
    }

    [Fact]
    public void Key_DoesNotContainTheRawIdentifier()
    {
        var key = CacheKeys.User(new GetUserByIdentifierQuery(
            Profile, Catalog, LdapIdentifierAttribute.SAMAccountName, "john.doe@example.com", LdapEntryAttributeSet.Minimum, true));

        Assert.DoesNotContain("john.doe", key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("example.com", key, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfileTag_IsNormalizedAndScoped()
    {
        Assert.Equal("ldap:profile:edu", CacheTags.Profile("  EDU "));
    }

    private static SearchUsersQuery Query(string filterValue, LdapEntryAttributeSet set = LdapEntryAttributeSet.Minimum) =>
        new(Profile, Catalog, LdapEntryAttribute.cn, filterValue, null, null, null, set);
}
