using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using MediatR;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfiles;

public sealed record GetProfilesQuery() : IRequest<Result<IReadOnlyList<LdapServerProfileDto>>>;

public sealed class GetProfilesQueryHandler : IRequestHandler<GetProfilesQuery, Result<IReadOnlyList<LdapServerProfileDto>>>
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfilesQueryHandler(IServerProfileReadService serverProfileReadService)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public async Task<Result<IReadOnlyList<LdapServerProfileDto>>> Handle(GetProfilesQuery request, CancellationToken cancellationToken)
   {
      var profiles = await _serverProfileReadService.GetProfilesAsync(cancellationToken);
      return Result<IReadOnlyList<LdapServerProfileDto>>.Success(profiles);
   }
}
