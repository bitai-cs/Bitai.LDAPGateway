using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;
using Bitai.LDAPGateway.Application.Authentications.Commands.AuthenticateWithoutUserLookup;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/{serverProfile:ldapSvrPf}/{catalogType:ldapCatType}/[controller]")]
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
        var credential = new UserCredential(request.Username, new Secret(request.Password));

        var result = await _mediator.Send(
            new AuthenticateCommand(serverProfile, catalogType, credential),
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
        var credential = new UserCredential(request.Username, new Secret(request.Password));

        var result = await _mediator.Send(
            new AuthenticateWithoutUserLookupCommand(serverProfile, catalogType, credential),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

public sealed record AuthenticateRequest(string Username, string Password);
