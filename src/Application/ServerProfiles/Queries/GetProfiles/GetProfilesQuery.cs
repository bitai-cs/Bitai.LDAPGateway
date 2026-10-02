using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfiles;

public sealed record GetProfilesQuery();

public sealed class GetProfilesQueryHandler
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfilesQueryHandler(IServerProfileReadService serverProfileReadService)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public async Task<Result<IReadOnlyList<LdapServerProfileDto>>> Handle(GetProfilesQuery request, CancellationToken cancellationToken)
   {
      var profiles = _serverProfileReadService.GetProfiles();
      return Result<IReadOnlyList<LdapServerProfileDto>>.Success(profiles);
   }
}
