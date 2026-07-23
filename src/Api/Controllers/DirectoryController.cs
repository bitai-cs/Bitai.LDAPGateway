using Bitai.LDAPGateway.Api.Extensions;
using Bitai.LDAPGateway.Application.Directory.Commands.CreateMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.DeleteMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.DisableMsAdUser;
using Bitai.LDAPGateway.Application.Directory.Commands.SetMsAdUserPassword;
using Bitai.LDAPGateway.Application.Directory.Queries.GetDirectoryEntryByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupByIdentifier;
using Bitai.LDAPGateway.Application.Directory.Queries.GetGroupParents;
using Bitai.LDAPGateway.Application.Directory.Queries.GetUserParents;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchDirectory;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchGroups;
using Bitai.LDAPGateway.Application.Directory.Queries.SearchUsers;
using Bitai.LDAPGateway.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Bitai.LDAPGateway.Api.Controllers;

[ApiController]
[Route("api/{serverProfile}/{catalogType}/[controller]")]
public sealed class DirectoryController : ControllerBase
{
   private readonly IMediator _mediator;

   public DirectoryController(IMediator mediator)
   {
      _mediator = mediator;
   }

   [HttpGet("{identifier}")]
   public async Task<IActionResult> GetByIdentifier(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      [FromQuery] string identifierAttribute = "distinguishedName",
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(
         new GetDirectoryEntryByIdentifierQuery(serverProfile, catalogType, identifier, identifierAttribute),
         cancellationToken);

      return this.ToActionResult(result);
   }

   [HttpGet("filterBy")]
   public async Task<IActionResult> FilterBy(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromQuery] string filter,
      [FromQuery] int sizeLimit = 100,
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(new SearchDirectoryQuery(serverProfile, catalogType, filter, sizeLimit), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpPost("MsADUsers")]
   public async Task<IActionResult> CreateMsAdUser(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromBody] CreateMsAdUserRequest request,
      CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(
         new CreateMsAdUserCommand(
            serverProfile,
            catalogType,
            request.distinguishedName,
            request.distinguishedNameOfContainer,
            request.givenName,
            request.sn,
            request.cn,
            request.name,
            request.displayName,
            request.description,
            request.objectClass,
            request.samAccountName,
            request.userPrincipalName,
            request.userAccountControl,
            request.department,
            request.telephoneNumber,
            request.mail,
            request.password),
         cancellationToken);

      return this.ToActionResult(result);
   }

   [HttpPatch("MsADUsers/{identifier}/Credential")]
   public async Task<IActionResult> SetCredential(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      [FromBody] SetMsAdUserCredentialRequest request,
      CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(
         new SetMsAdUserPasswordCommand(serverProfile, catalogType, identifier, request.Password, request.MustChangeAtNextLogon),
         cancellationToken);

      return this.ToActionResult(result);
   }

   [HttpPatch("MsADUsers/{identifier}/disableBy")]
   public async Task<IActionResult> DisableBy(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      [FromBody] DisableMsAdUserRequest request,
      CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(new DisableMsAdUserCommand(serverProfile, catalogType, identifier, request.Reason), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpDelete("MsADUsers/{identifier}")]
   public async Task<IActionResult> Delete(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      CancellationToken cancellationToken)
   {
      var result = await _mediator.Send(new DeleteMsAdUserCommand(serverProfile, catalogType, identifier), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("Users/{identifier}/Parents")]
   public async Task<IActionResult> GetUserParents(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      [FromQuery] string identifierAttribute = "distinguishedName",
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(new GetUserParentsQuery(serverProfile, catalogType, identifier, identifierAttribute), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("Users/filterBy")]
   public async Task<IActionResult> FilterUsersBy(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromQuery] string filter,
      [FromQuery] int sizeLimit = 100,
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(new SearchUsersQuery(serverProfile, catalogType, filter, sizeLimit), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("Groups/{identifier}")]
   public async Task<IActionResult> GetGroup(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      [FromQuery] string identifierAttribute = "distinguishedName",
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(new GetGroupByIdentifierQuery(serverProfile, catalogType, identifier, identifierAttribute), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("Groups/{identifier}/Parents")]
   public async Task<IActionResult> GetGroupParents(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromRoute] string identifier,
      [FromQuery] string identifierAttribute = "distinguishedName",
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(new GetGroupParentsQuery(serverProfile, catalogType, identifier, identifierAttribute), cancellationToken);
      return this.ToActionResult(result);
   }

   [HttpGet("Groups/filterBy")]
   public async Task<IActionResult> FilterGroupsBy(
      [FromRoute] string serverProfile,
      [FromRoute] CatalogType catalogType,
      [FromQuery] string filter,
      [FromQuery] int sizeLimit = 100,
      CancellationToken cancellationToken = default)
   {
      var result = await _mediator.Send(new SearchGroupsQuery(serverProfile, catalogType, filter, sizeLimit), cancellationToken);
      return this.ToActionResult(result);
   }
}

public sealed record CreateMsAdUserRequest(
   string distinguishedName,
   string distinguishedNameOfContainer,
   string givenName,
   string sn,
   string cn,
   string name,
   string displayName,
   string description,
   string[] objectClass,
   string samAccountName,
   string userPrincipalName,
   string userAccountControl,
   string department,
   string telephoneNumber,
   string mail,
   string password);

public sealed record SetMsAdUserCredentialRequest(string Password, bool MustChangeAtNextLogon = false);

public sealed record DisableMsAdUserRequest(string? Reason);
