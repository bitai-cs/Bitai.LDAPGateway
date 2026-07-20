using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileById;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfiles;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ServerProfilesController : ControllerBase
{
   private readonly IMediator _mediator;

   public ServerProfilesController(IMediator mediator)
   {
      _mediator = mediator;
   }

   [HttpGet("GetProfileIds")]
   public async Task<IActionResult> GetProfileIds(CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(new GetProfileIdsQuery(), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("{profileId}")]
   public async Task<IActionResult> GetById([FromRoute] string profileId, CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(new GetProfileByIdQuery(profileId), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet]
   public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(new GetProfilesQuery(), cancellationToken);
      return this.ToActionResult(result);
   }
}
