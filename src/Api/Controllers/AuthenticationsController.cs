using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;
using Bitai.LDAPGateway.Application.Authentications.Commands.AuthenticateWithoutUserLookup;
using Bitai.LDAPGateway.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/{serverProfile}/{catalogType}/[controller]")]
public sealed class AuthenticationsController : ControllerBase
{
   private readonly IMediator _mediator;

   public AuthenticationsController(IMediator mediator)
   {
      _mediator = mediator;
   }

   [HttpPost("authenticate")]
   public async Task<IActionResult> Authenticate(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromBody] AuthenticateRequest request,
      CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(
         new AuthenticateCommand(serverProfile, catalogType, request.Username, request.Password),
         cancellationToken);

      return this.ToActionResult(result);
   }

   [HttpPost("authenticateWithoutUserLookup")]
   public async Task<IActionResult> AuthenticateWithoutUserLookup(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromBody] AuthenticateRequest request,
      CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(
         new AuthenticateWithoutUserLookupCommand(serverProfile, catalogType, request.Username, request.Password),
         cancellationToken);

      return this.ToActionResult(result);
   }
}

public sealed record AuthenticateRequest(string Username, string Password);
