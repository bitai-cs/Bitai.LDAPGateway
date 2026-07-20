using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using MediatR;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;

public sealed record GetProfileIdsQuery() : IRequest<Result<IReadOnlyList<string>>>;

public sealed class GetProfileIdsQueryHandler : IRequestHandler<GetProfileIdsQuery, Result<IReadOnlyList<string>>>
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfileIdsQueryHandler(IServerProfileReadService serverProfileReadService)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public async Task<Result<IReadOnlyList<string>>> Handle(GetProfileIdsQuery request, CancellationToken cancellationToken)
   {
      var items = await _serverProfileReadService.GetProfileIdsAsync(cancellationToken);
      return Result<IReadOnlyList<string>>.Success(items);
   }
}
