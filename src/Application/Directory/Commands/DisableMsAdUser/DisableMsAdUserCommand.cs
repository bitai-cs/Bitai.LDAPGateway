using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Commands.DisableMsAdUser;

public sealed record DisableMsAdUserCommand(
   string ServerProfile,
   CatalogType CatalogType,
   string Identifier,
   string? Reason) : IRequest<Result>;

public sealed class DisableMsAdUserCommandValidator : AbstractValidator<DisableMsAdUserCommand>
{
   public DisableMsAdUserCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
   }
}

public sealed class DisableMsAdUserCommandHandler : LdapHandlerBase, IRequestHandler<DisableMsAdUserCommand, Result>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public DisableMsAdUserCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result> Handle(DisableMsAdUserCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("DisableMsAdUser", context,
         () => _ldapGatewayClient.DisableMsAdUserAsync(context, request.Identifier, request.Reason, cancellationToken),
         cancellationToken);
   }
}
