using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class LdapServerProfileReadService : IServerProfileReadService
{
   private readonly IOptionsMonitor<LdapServerProfilesOptions> _options;

   public LdapServerProfileReadService(IOptionsMonitor<LdapServerProfilesOptions> options)
   {
      _options = options;
   }

   public Task<IReadOnlyList<string>> GetProfileIdsAsync(CancellationToken cancellationToken)
   {
      IReadOnlyList<string> ids = _options.CurrentValue.Select(x => x.ProfileId).ToArray();
      return Task.FromResult(ids);
   }

   public Task<IReadOnlyList<LdapServerProfileDto>> GetProfilesAsync(CancellationToken cancellationToken)
   {
      IReadOnlyList<LdapServerProfileDto> profiles = _options.CurrentValue.Select(Map).ToArray();
      return Task.FromResult(profiles);
   }

   public Task<LdapServerProfileDto?> GetProfileAsync(string profileId, CancellationToken cancellationToken)
   {
      var profile = _options.CurrentValue
         .FirstOrDefault(x => string.Equals(x.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

      return Task.FromResult(profile is null ? null : Map(profile));
   }

   private static LdapServerProfileDto Map(LdapServerProfileOption value)
   {
      return new LdapServerProfileDto(
         value.ProfileId,
         value.Server,
         value.Port,
         value.PortForGlobalCatalog,
         value.BaseDN,
         value.BaseDNforGlobalCatalog,
         value.DefaultDomainName,
         value.ConnectionTimeout,
         value.UseSSL,
         value.UseSSLforGlobalCatalog,
         value.BindAccountName,
         value.HealthCheckPingTimeout);
   }
}
