using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Application.Common.Interfaces;

namespace Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;

public sealed record GetProfileIdsQuery();

/// <summary>Handles <see cref="GetProfileIdsQuery"/>.</summary>
public sealed class GetProfileIdsQueryHandler : LdapHandlerBase
{
   private readonly IServerProfileReadService _serverProfileReadService;

   public GetProfileIdsQueryHandler(IServerProfileReadService serverProfileReadService, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _serverProfileReadService = serverProfileReadService;
   }

   public Task<Result<IReadOnlyList<string>>> Handle(GetProfileIdsQuery request, CancellationToken cancellationToken)
   {
      return ExecuteWithoutCatalogAsync("GetProfileIds", null, () =>
         Task.FromResult(Result<IReadOnlyList<string>>.Success(_serverProfileReadService.GetProfileIds())), cancellationToken);
   }
}
