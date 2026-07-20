namespace Bitai.LDAPGateway.Domain.Entities;

public sealed record LdapServerProfile(
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
   string DomainAccountPassword,
   int HealthCheckPingTimeout);
