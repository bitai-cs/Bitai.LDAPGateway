using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.CatalogTypes.Queries.GetCatalogTypes;

public sealed record GetCatalogTypesQuery();

/// <summary>Handles <see cref="GetCatalogTypesQuery"/>.</summary>
public sealed class GetCatalogTypesQueryHandler : LdapHandlerBase
{
   public GetCatalogTypesQueryHandler(IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
   }

   public Task<Result<IReadOnlyList<string>>> Handle(GetCatalogTypesQuery request, CancellationToken cancellationToken)
   {
      IReadOnlyList<string> values = ["LC", "GC"];
      return ExecuteWithoutCatalogAsync("GetCatalogTypes", null, () =>
         Task.FromResult(Result<IReadOnlyList<string>>.Success(values)), cancellationToken);
   }
}
