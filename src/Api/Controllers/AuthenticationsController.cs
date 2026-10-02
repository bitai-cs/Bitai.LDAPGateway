using Bitai.LDAPGateway.Application.Common.Models;
using Wolverine;
using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;
using Bitai.LDAPGateway.Application.Authentications.Commands.AuthenticateWithoutUserLookup;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/{serverProfile:ldapSvrPf}/{catalogType:ldapCatType}/[controller]")]
public sealed class AuthenticationsController : ControllerBase
{
    private readonly IMessageBus _bus;

    public AuthenticationsController(IMessageBus bus)
    {
        _bus = bus;
    }

    [HttpPost("authenticate")]
    public async Task<IActionResult> Authenticate(
        [FromRoute] string serverProfile,
        [FromRoute] CatalogType catalogType,
        [FromBody] AuthenticateRequest request,
        CancellationToken cancellationToken)
    {
        var credential = new UserCredential(request.Username, new Secret(request.Password));

        var result = await _bus.InvokeAsync<Result<AuthenticationResultDto>>(
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

        var result = await _bus.InvokeAsync<Result<AuthenticationResultDto>>(
            new AuthenticateWithoutUserLookupCommand(serverProfile, catalogType, credential),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

public sealed record AuthenticateRequest(string Username, string Password);
