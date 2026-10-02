using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Directory.Commands.DisableMsAdUser;

public sealed record DisableMsAdUserCommand(
   string ServerProfile,
   CatalogType CatalogType,
   LdapIdentifierAttribute IdentifierAttribute,
   string Identifier,
   string? Reason);

public sealed class DisableMsAdUserCommandValidator : AbstractValidator<DisableMsAdUserCommand>
{
   public DisableMsAdUserCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
   }
}

public sealed class DisableMsAdUserCommandHandler : LdapHandlerBase
{
   private readonly IDirectoryServiceConnector _directoryServiceConnector;

   public DisableMsAdUserCommandHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _directoryServiceConnector = directoryServiceConnector;
   }

   public Task<Result> Handle(DisableMsAdUserCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("DisableMsAdUser", context,
         () => _directoryServiceConnector.DisableMsAdUserAsync(context, request.IdentifierAttribute, request.Identifier, request.Reason, cancellationToken),
         cancellationToken);
   }
}
