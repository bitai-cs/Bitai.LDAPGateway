using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Commands.SetMsAdUserPassword;

public sealed record SetMsAdUserPasswordCommand(
   string ServerProfile,
   CatalogType CatalogType,
   string Identifier,
   string NewPassword,
   bool MustChangeAtNextLogon) : IRequest<Result>;

public sealed class SetMsAdUserPasswordCommandValidator : AbstractValidator<SetMsAdUserPasswordCommand>
{
   public SetMsAdUserPasswordCommandValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
      RuleFor(x => x.NewPassword).NotEmpty();
   }
}

public sealed class SetMsAdUserPasswordCommandHandler : LdapHandlerBase, IRequestHandler<SetMsAdUserPasswordCommand, Result>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public SetMsAdUserPasswordCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result> Handle(SetMsAdUserPasswordCommand request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("SetMsAdUserPassword", context,
         () => _ldapGatewayClient.SetMsAdUserPasswordAsync(context, request.Identifier, request.NewPassword, request.MustChangeAtNextLogon, cancellationToken),
         cancellationToken);
   }
}
