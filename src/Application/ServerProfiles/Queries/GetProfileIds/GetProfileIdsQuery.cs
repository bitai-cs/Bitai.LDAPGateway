using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Common.Interfaces;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;

public sealed record GetProfileIdsQuery();

public sealed class GetProfileIdsQueryHandler
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfileIdsQueryHandler(IServerProfileReadService serverProfileReadService)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public async Task<Result<IReadOnlyList<string>>> Handle(GetProfileIdsQuery request, CancellationToken cancellationToken)
   {
      var items = _serverProfileReadService.GetProfileIds();
      return Result<IReadOnlyList<string>>.Success(items);
   }
}
