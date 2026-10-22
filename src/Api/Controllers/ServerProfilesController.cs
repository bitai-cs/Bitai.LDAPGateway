using Bitai.LDAPGateway.Application.Common.Models;
using Wolverine;
using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileById;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfileIds;
using Bitai.LDAPGateway.Application.ServerProfiles.Queries.GetProfiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Bitai.LDAPGateway.Api.Authorization;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ServerProfilesController : ControllerBase
{
   private readonly IMessageBus _bus;

   public ServerProfilesController(IMessageBus bus)
   {
      _bus = bus;
   }

   [HttpGet("GetProfileIds")]
   [Authorize(Policy = ApiAuthorizationPolicies.ReaderOrAdministrator)]
   public async Task<IActionResult> GetProfileIds(CancellationToken cancellationToken)
   {
      var result = await _bus.InvokeAsync<Result<IReadOnlyList<string>>>(new GetProfileIdsQuery(), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("{profileId}")]
   [Authorize(Policy = ApiAuthorizationPolicies.ReaderOrAdministrator)]
   public async Task<IActionResult> GetById([FromRoute] string profileId, CancellationToken cancellationToken)
   {
      var result = await _bus.InvokeAsync<Result<LdapServerProfileDto>>(new GetProfileByIdQuery(profileId), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet]
   [Authorize(Policy = ApiAuthorizationPolicies.ReaderOrAdministrator)]
   public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
   {
      var result = await _bus.InvokeAsync<Result<IReadOnlyList<LdapServerProfileDto>>>(new GetProfilesQuery(), cancellationToken);
      return this.ToActionResult(result);
   }
}
