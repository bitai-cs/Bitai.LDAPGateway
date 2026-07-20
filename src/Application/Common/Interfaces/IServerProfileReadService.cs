using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.Common.Interfaces;

public interface IServerProfileReadService
{
   Task<IReadOnlyList<string>> GetProfileIdsAsync(CancellationToken cancellationToken);
   Task<IReadOnlyList<LdapServerProfileDto>> GetProfilesAsync(CancellationToken cancellationToken);
   Task<LdapServerProfileDto?> GetProfileAsync(string profileId, CancellationToken cancellationToken);
}
