using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Directory.Commands.DeleteMsAdUser;

public sealed record DeleteMsAdUserCommand(
   string ServerProfile,
   CatalogType CatalogType,
   LdapIdentifierAttribute IdentifierAttribute,
   string Identifier);

public sealed class DeleteMsAdUserCommandValidator : AbstractValidator<DeleteMsAdUserCommand>
{
   public DeleteMsAdUserCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
   }
}

public sealed class DeleteMsAdUserCommandHandler : LdapHandlerBase
{
   private readonly IDirectoryServiceConnector _directoryServiceConnector;

   public DeleteMsAdUserCommandHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _directoryServiceConnector = directoryServiceConnector;
   }

   public Task<Result> Handle(DeleteMsAdUserCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("DeleteMsAdUser", context,
         () => _directoryServiceConnector.DeleteMsAdUserAsync(context, request.IdentifierAttribute, request.Identifier, cancellationToken),
         cancellationToken);
   }
}
