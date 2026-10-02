using Bitai.LDAPGateway.Application.Common.Models;

namespace Bitai.LDAPGateway.Application.CatalogTypes.Queries.GetCatalogTypes;

public sealed record GetCatalogTypesQuery();

public sealed class GetCatalogTypesQueryHandler
{
   public Task<Result<IReadOnlyList<string>>> Handle(GetCatalogTypesQuery request, CancellationToken cancellationToken)
   {
      IReadOnlyList<string> values = ["LC", "GC"];
      return Task.FromResult(Result<IReadOnlyList<string>>.Success(values));
   }
}
