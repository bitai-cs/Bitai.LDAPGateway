using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfiles;

public sealed record GetProfilesQuery();

/// <summary>Handles <see cref="GetProfilesQuery"/>.</summary>
public sealed class GetProfilesQueryHandler : LdapHandlerBase
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfilesQueryHandler(IServerProfileReadService serverProfileReadService, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public Task<Result<IReadOnlyList<LdapServerProfileDto>>> Handle(GetProfilesQuery request, CancellationToken cancellationToken)
   {
      return ExecuteWithoutCatalogAsync("GetProfiles", null, () =>
         Task.FromResult(Result<IReadOnlyList<LdapServerProfileDto>>.Success(_serverProfileReadService.GetProfiles())), cancellationToken);
   }
}
