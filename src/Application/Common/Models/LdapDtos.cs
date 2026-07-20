namespace Bitai.LDAPGateway.Application.Common.Models;

public sealed record AuthenticationResultDto(bool Authenticated, string Username, string Message);

public sealed record DirectoryEntryDto(string Identifier, IReadOnlyDictionary<string, string> Attributes);

public sealed record LdapUserDto(string Identifier, string Username, string DisplayName, string Email);

public sealed record LdapGroupDto(string Identifier, string Name, string Description);

public sealed record CreateMsAdUserDto(
   string SamAccountName,
   string UserPrincipalName,
   string GivenName,
   string Surname,
   string DisplayName,
   string OrganizationalUnitDn,
   string InitialPassword,
   bool Enabled);

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
   string DomainAccountName,
   int HealthCheckPingTimeout);
