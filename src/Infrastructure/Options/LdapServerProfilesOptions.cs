namespace Bitai.LDAPGateway.Infrastructure.Options;

public sealed class LdapServerProfilesOptions : List<LdapServerProfileOption>
{
   public const string SectionName = "LDAPServerProfiles";
}

public sealed class LdapServerProfileOption
{
   public string ProfileId { get; set; } = string.Empty;
   public string Server { get; set; } = string.Empty;
   public string Port { get; set; } = "Default";
   public string PortForGlobalCatalog { get; set; } = "Default";
   public string BaseDN { get; set; } = string.Empty;
   public string BaseDNforGlobalCatalog { get; set; } = string.Empty;
   public string DefaultDomainName { get; set; } = string.Empty;
   public int ConnectionTimeout { get; set; } = 10;
   public string UseSSL { get; set; } = "false";
   public string UseSSLforGlobalCatalog { get; set; } = "false";
   public string DomainAccountName { get; set; } = string.Empty;
   public string DomainAccountPassword { get; set; } = string.Empty;
   public int HealthCheckPingTimeout { get; set; } = 100;
}
