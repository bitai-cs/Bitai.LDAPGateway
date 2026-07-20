using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.CatalogTypes.Queries.GetCatalogTypes;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CatalogTypesController : ControllerBase
{
   private readonly IMediator _mediator;

   public CatalogTypesController(IMediator mediator)
   {
      _mediator = mediator;
   }

   [HttpGet]
   public async Task<IActionResult> Get(CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(new GetCatalogTypesQuery(), cancellationToken);
      return this.ToActionResult(result);
   }
}
