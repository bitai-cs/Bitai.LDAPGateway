namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record AuthenticationResultDto(bool Authenticated, string Username, string Message);

public sealed record DirectoryEntryDto(string Identifier, IReadOnlyDictionary<string, string> Attributes);

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
