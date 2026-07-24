namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record AuthenticationResultDto(bool Authenticated, string Username, string Message);

public sealed record DirectoryEntryDto
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
    public IEnumerable<DirectoryEntryDto>? memberOfEntries { get; init; }
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

public sealed record LdapUserDto(string Identifier, string Username, string DisplayName, string Email);

public sealed record LdapGroupDto(string Identifier, string Name, string Description);

public record CreateMsAdUserDto: Bitai.LDAPHelper.DTO.LDAPMsADUserAccount
{
    public CreateMsAdUserDto() : base() { }

    public CreateMsAdUserDto(string distinguishedNameOfContainer)
        : base(distinguishedNameOfContainer) { }

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
        : base(distinguishedNameOfContainer, givenName, sn, cn, name, displayName, description,
               distinguishedName, objectClass, samAccountName, userPrincipalName, userAccountControl,
               department, telephoneNumber, mail, password) { }
}
// public sealed record CreateMsAdUserDto(
//    string DistinguishedName,
//    string DistinguishedNameOfContainer,
//    string GivenName,
//    string Surname,
//    string CommonName,
//    string Name,
//    string DisplayName,
//    string Description,
//    string[] ObjectClass,
//    string SamAccountName,
//    string UserPrincipalName,
//    string UserAccountControl,
//    string Department,
//    string TelephoneNumber,
//    string Mail,
//    string Password);

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
