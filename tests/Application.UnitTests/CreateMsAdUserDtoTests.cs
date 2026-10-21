using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.UnitTests;

public sealed class CreateMsAdUserDtoTests
{
    [Fact]
    public void Constructor_WithContainer_ShouldSetDefaultAccountControl()
    {
        var user = new CreateMsAdUserDto("OU=Users,DC=example,DC=com");

        Assert.Equal("OU=Users,DC=example,DC=com", user.DistinguishedNameOfContainer);
        Assert.Equal("NORMAL_ACCOUNT,DONT_EXPIRE_PASSWORD", user.UserAccountControl);
    }

    [Fact]
    public void Constructor_WithValues_ShouldPreserveAllProvidedValues()
    {
        var objectClass = new[] { "top", "person", "user" };

        var user = new CreateMsAdUserDto(
            "OU=Users,DC=example,DC=com",
            "Ada",
            "Lovelace",
            "Ada Lovelace",
            "Ada Lovelace",
            "Ada Lovelace",
            "First programmer",
            "CN=Ada Lovelace,OU=Users,DC=example,DC=com",
            objectClass,
            "alovelace",
            "alovelace@example.com",
            "NORMAL_ACCOUNT",
            "Engineering",
            "+1-555-0100",
            "ada@example.com",
            "secret");

        Assert.Equal("Ada", user.GivenName);
        Assert.Equal("Lovelace", user.Sn);
        Assert.Equal("Ada Lovelace", user.Cn);
        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("Ada Lovelace", user.DisplayName);
        Assert.Equal("First programmer", user.Description);
        Assert.Equal("CN=Ada Lovelace,OU=Users,DC=example,DC=com", user.DistinguishedName);
        Assert.Same(objectClass, user.ObjectClass);
        Assert.Equal("alovelace", user.SAMAccountName);
        Assert.Equal("alovelace@example.com", user.UserPrincipalName);
        Assert.Equal("NORMAL_ACCOUNT", user.UserAccountControl);
        Assert.Equal("Engineering", user.Department);
        Assert.Equal("+1-555-0100", user.TelephoneNumber);
        Assert.Equal("ada@example.com", user.Mail);
        Assert.Equal("secret", user.Password);
    }
}
