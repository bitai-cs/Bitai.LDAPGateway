using Bitai.LDAPGateway.Application.Common.Models;
using MediatR;

namespace Bitai.LDAPGateway.Application.CatalogTypes.Queries.GetCatalogTypes;

public sealed record GetCatalogTypesQuery() : IRequest<Result<IReadOnlyList<string>>>;

public sealed class GetCatalogTypesQueryHandler : IRequestHandler<GetCatalogTypesQuery, Result<IReadOnlyList<string>>>
{
   public Task<Result<IReadOnlyList<string>>> Handle(GetCatalogTypesQuery request, CancellationToken cancellationToken)
   {
      IReadOnlyList<string> values = ["LC", "GC"];
      return Task.FromResult(Result<IReadOnlyList<string>>.Success(values));
   }
}
