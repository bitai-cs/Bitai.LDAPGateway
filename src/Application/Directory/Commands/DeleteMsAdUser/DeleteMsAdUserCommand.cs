using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Commands.DeleteMsAdUser;

public sealed record DeleteMsAdUserCommand(
   string ServerProfile,
   CatalogType CatalogType,
   string Identifier) : IRequest<Result>;

public sealed class DeleteMsAdUserCommandValidator : AbstractValidator<DeleteMsAdUserCommand>
{
   public DeleteMsAdUserCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
   }
}

public sealed class DeleteMsAdUserCommandHandler : LdapHandlerBase, IRequestHandler<DeleteMsAdUserCommand, Result>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public DeleteMsAdUserCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result> Handle(DeleteMsAdUserCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("DeleteMsAdUser", context,
         () => _ldapGatewayClient.DeleteMsAdUserAsync(context, request.Identifier, cancellationToken),
         cancellationToken);
   }
}
