using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Authentications.Commands.AuthenticateWithoutUserLookup;

public sealed record AuthenticateWithoutUserLookupCommand(
   string ServerProfile,
   CatalogType CatalogType,
   string Username,
   string Password) : IRequest<Result<AuthenticationResultDto>>;

public sealed class AuthenticateWithoutUserLookupCommandValidator : AbstractValidator<AuthenticateWithoutUserLookupCommand>
{
   public AuthenticateWithoutUserLookupCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Username).NotEmpty();
      RuleFor(x => x.Password).NotEmpty();
   }
}

public sealed class AuthenticateWithoutUserLookupCommandHandler : LdapHandlerBase, IRequestHandler<AuthenticateWithoutUserLookupCommand, Result<AuthenticationResultDto>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public AuthenticateWithoutUserLookupCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<AuthenticationResultDto>> Handle(AuthenticateWithoutUserLookupCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync(
         operationName: "AuthenticateWithoutUserLookup",
         context,
         () => _ldapGatewayClient.AuthenticateWithoutUserLookupAsync(context, request.Username, request.Password, cancellationToken),
         cancellationToken);
   }
}
