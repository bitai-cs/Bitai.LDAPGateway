using Bitai.LDAPGateway.Application.Common.Models;
using Wolverine;
using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.CatalogTypes.Queries.GetCatalogTypes;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CatalogTypesController : ControllerBase
{
   private readonly IMessageBus _bus;

   public CatalogTypesController(IMessageBus bus)
   {
      _bus = bus;
   }

   [HttpGet]
   public async Task<IActionResult> Get(CancellationToken cancellationToken)
   {
      var result = await _bus.InvokeAsync<Result<IReadOnlyList<string>>>(new GetCatalogTypesQuery(), cancellationToken);
      return this.ToActionResult(result);
   }
}
