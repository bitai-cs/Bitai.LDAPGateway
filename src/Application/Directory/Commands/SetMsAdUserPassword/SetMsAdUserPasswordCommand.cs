using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Directory.Commands.SetMsAdUserPassword;

public sealed record SetMsAdUserPasswordCommand(
   string ServerProfile,
   CatalogType CatalogType,
   LdapIdentifierAttribute IdentifierAttribute,
   string Identifier,
   string NewPassword,
   bool MustChangeAtNextLogon)
{
   // Wolverine logs the message (ToString) when a handler fails: never print the password.
   private bool PrintMembers(System.Text.StringBuilder builder)
   {
      builder.Append($"ServerProfile = {ServerProfile}, CatalogType = {CatalogType}, IdentifierAttribute = {IdentifierAttribute}, Identifier = {Identifier}, NewPassword = ***, MustChangeAtNextLogon = {MustChangeAtNextLogon}");
      return true;
   }
};

public sealed class SetMsAdUserPasswordCommandValidator : AbstractValidator<SetMsAdUserPasswordCommand>
{
   public SetMsAdUserPasswordCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
      RuleFor(x => x.NewPassword).NotEmpty();
   }
}

public sealed class SetMsAdUserPasswordCommandHandler : LdapHandlerBase
{
   private readonly IDirectoryServiceConnector _directoryServiceConnector;

   public SetMsAdUserPasswordCommandHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _directoryServiceConnector = directoryServiceConnector;
   }

   public Task<Result> Handle(SetMsAdUserPasswordCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("SetMsAdUserPassword", context,
         () => _directoryServiceConnector.SetMsAdUserPasswordAsync(context, request.IdentifierAttribute, request.Identifier, request.NewPassword, request.MustChangeAtNextLogon, cancellationToken),
         cancellationToken);
   }
}
