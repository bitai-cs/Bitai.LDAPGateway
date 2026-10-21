namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record AuthenticationResultDto(bool Authenticated, string Username, string Message);

public sealed record LdapEntryDto
{
    public string? RequestLabel { get; init; }
    public string? c { get; init; }
    public string? cn { get; init; }
    public string? company { get; init; }
    public string? co { get; init; }
    public string? description { get; init; }
    public string? department { get; init; }
    public string? displayName { get; init; }
    public string? distinguishedName { get; init; }
    public string? givenName { get; init; }
    public string? l { get; init; }
    public DateTime? lastLogon { get; init; }
    public string? mail { get; init; }
    public string? manager { get; init; }
    public string[]? member { get; init; }
    public string[]? memberOf { get; init; }
    public IEnumerable<LdapEntryDto>? memberOfEntries { get; init; }
    public string? name { get; init; }
    public string? objectCategory { get; init; }
    public string[]? objectClass { get; init; }
    public string? samAccountName { get; init; }
    public string? samAccountType { get; init; }
    public string? sn { get; init; }
    public string? telephoneNumber { get; init; }
    public string? title { get; init; }
    public string? userPrincipalName { get; init; }
    public DateTime? whenCreated { get; init; }
    public string? objectGuid { get; init; }
    public byte[]? objectGuidBytes { get; init; }
    public string? objectSid { get; init; }
    public byte[]? objectSidBytes { get; init; }
    public string? userAccountControl { get; init; }
}

public record CreateMsAdUserDto
{
    private const string DefaultUserAccountControl = "NORMAL_ACCOUNT,DONT_EXPIRE_PASSWORD";

    public CreateMsAdUserDto()
    {
        UserAccountControl = DefaultUserAccountControl;
    }

    public CreateMsAdUserDto(string distinguishedNameOfContainer)
        : this()
    {
        DistinguishedNameOfContainer = distinguishedNameOfContainer
            ?? throw new ArgumentNullException(nameof(distinguishedNameOfContainer));
    }

    public CreateMsAdUserDto(
        string distinguishedNameOfContainer,
        string? givenName = null,
        string? sn = null,
        string? cn = null,
        string? name = null,
        string? displayName = null,
        string? description = null,
        string? distinguishedName = null,
        string[]? objectClass = null,
        string? samAccountName = null,
        string? userPrincipalName = null,
        string? userAccountControl = null,
        string? department = null,
        string? telephoneNumber = null,
        string? mail = null,
        string? password = null)
        : this(distinguishedNameOfContainer)
    {
        GivenName = givenName;
        Sn = sn;
        Cn = cn;
        Name = name;
        DisplayName = displayName;
        Description = description;
        DistinguishedName = distinguishedName;
        ObjectClass = objectClass;
        SAMAccountName = samAccountName;
        UserPrincipalName = userPrincipalName;
        Department = department;
        TelephoneNumber = telephoneNumber;
        Mail = mail;
        Password = password;

        if (userAccountControl is not null)
        {
            UserAccountControl = userAccountControl;
        }
    }

    public string? DistinguishedNameOfContainer { get; init; }
    public string? GivenName { get; init; }
    public string? Sn { get; init; }
    public string? Cn { get; init; }
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? DistinguishedName { get; init; }
    public string[]? ObjectClass { get; init; }
    public string? SAMAccountName { get; init; }
    public string? UserPrincipalName { get; init; }
    public string? UserAccountControl { get; init; }
    public string? Department { get; init; }
    public string? TelephoneNumber { get; init; }
    public string? Mail { get; init; }
    public string? Password { get; init; }
}

public sealed record LdapServerProfileDto(
    string ProfileId,
    string Server,
    string Port,
    string PortForGlobalCatalog,
    string BaseDn,
    string BaseDnForGlobalCatalog,
    string DefaultDomainName,
    int ConnectionTimeout,
    bool UseSsl,
    bool UseSslForGlobalCatalog,
    string BindAccountName,
    int HealthCheckPingTimeout);
