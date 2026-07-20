using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetGroupByIdentifier;

public sealed record GetGroupByIdentifierQuery(
   string ServerProfile,
   CatalogType CatalogType,
   string Identifier,
   string IdentifierAttribute) : IRequest<Result<LdapGroupDto>>;

public sealed class GetGroupByIdentifierQueryValidator : AbstractValidator<GetGroupByIdentifierQuery>
{
   public GetGroupByIdentifierQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
      RuleFor(x => x.IdentifierAttribute).NotEmpty();
   }
}

public sealed class GetGroupByIdentifierQueryHandler : LdapHandlerBase, IRequestHandler<GetGroupByIdentifierQuery, Result<LdapGroupDto>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public GetGroupByIdentifierQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<LdapGroupDto>> Handle(GetGroupByIdentifierQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("GetGroupByIdentifier", context,
         () => _ldapGatewayClient.GetGroupAsync(context, request.Identifier, request.IdentifierAttribute, cancellationToken),
         cancellationToken);
   }
}
