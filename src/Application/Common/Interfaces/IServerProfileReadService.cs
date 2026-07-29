using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.Common.Interfaces;

public interface IServerProfileReadService
{
    IReadOnlyList<string> GetProfileIds();
    IReadOnlyList<LdapServerProfileDto> GetProfiles();
    LdapServerProfileDto? GetProfile(string profileId);
}
