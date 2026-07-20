using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Commands.CreateMsAdUser;

public sealed record CreateMsAdUserCommand(
   string ServerProfile,
   CatalogType CatalogType,
   string SamAccountName,
   string UserPrincipalName,
   string GivenName,
   string Surname,
   string DisplayName,
   string OrganizationalUnitDn,
   string InitialPassword,
   bool Enabled) : IRequest<Result<DirectoryEntryDto>>;

public sealed class CreateMsAdUserCommandValidator : AbstractValidator<CreateMsAdUserCommand>
{
   public CreateMsAdUserCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.SamAccountName).NotEmpty();
      RuleFor(x => x.UserPrincipalName).NotEmpty();
      RuleFor(x => x.GivenName).NotEmpty();
      RuleFor(x => x.Surname).NotEmpty();
      RuleFor(x => x.OrganizationalUnitDn).NotEmpty();
      RuleFor(x => x.InitialPassword).NotEmpty();
   }
}

public sealed class CreateMsAdUserCommandHandler : LdapHandlerBase, IRequestHandler<CreateMsAdUserCommand, Result<DirectoryEntryDto>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public CreateMsAdUserCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<DirectoryEntryDto>> Handle(CreateMsAdUserCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      var payload = new CreateMsAdUserDto(
         request.SamAccountName,
         request.UserPrincipalName,
         request.GivenName,
         request.Surname,
         request.DisplayName,
         request.OrganizationalUnitDn,
         request.InitialPassword,
         request.Enabled);

      return ExecuteAsync("CreateMsAdUser", context,
         () => _ldapGatewayClient.CreateMsAdUserAsync(context, payload, cancellationToken),
         cancellationToken);
   }
}
