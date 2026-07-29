using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Infrastructure.Services;

/// <summary>
/// Provides read services for LDAP server profiles by retrieving configuration from options.
/// </summary>
public sealed class LdapServerProfileReadService : IServerProfileReadService
{
    private readonly IOptionsMonitor<LdapServerProfilesOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="LdapServerProfileReadService"/> class.
    /// </summary>
    /// <param name="options">The options monitor for LDAP server profiles.</param>
    public LdapServerProfileReadService(IOptionsMonitor<LdapServerProfilesOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Retrieves a list of all configured LDAP server profile IDs.
    /// </summary>
    /// <returns>A read-only list of LDAP server profile IDs.</returns>
    public IReadOnlyList<string> GetProfileIds()
    {
        return _options.CurrentValue.Select(x => x.ProfileId).ToArray();
    }

    /// <summary>
    /// Retrieves a list of all configured LDAP server profiles.
    /// </summary>
    /// <returns>A read-only list of LDAP server profiles as DTOs.</returns>
    public IReadOnlyList<LdapServerProfileDto> GetProfiles()
    {
        return _options.CurrentValue.Select(Map).ToArray();
    }

    /// <summary>
    /// Retrieves a specific LDAP server profile by its ID.
    /// </summary>
    /// <param name="profileId">The unique identifier of the LDAP server profile.</param>
    /// <returns>The <see cref="LdapServerProfileDto"/> if found, otherwise null.</returns>
    public LdapServerProfileDto? GetProfile(string profileId)
    {
        var profile = _options.CurrentValue
            .FirstOrDefault(x => string.Equals(x.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

        return profile is null ? null : Map(profile);
    }

    /// <summary>
    /// Maps an <see cref="LdapServerProfileOption"/> to an <see cref="LdapServerProfileDto"/>.
    /// </summary>
    /// <param name="value">The <see cref="LdapServerProfileOption"/> to map.</param>
    /// <returns>A new instance of <see cref="LdapServerProfileDto"/>.</returns>
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
