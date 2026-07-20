using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetGroupParents;

public sealed record GetGroupParentsQuery(
   string ServerProfile,
   CatalogType CatalogType,
   string Identifier,
   string IdentifierAttribute) : IRequest<Result<IReadOnlyList<LdapGroupDto>>>;

public sealed class GetGroupParentsQueryValidator : AbstractValidator<GetGroupParentsQuery>
{
   public GetGroupParentsQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
      RuleFor(x => x.IdentifierAttribute).NotEmpty();
   }
}

public sealed class GetGroupParentsQueryHandler : LdapHandlerBase, IRequestHandler<GetGroupParentsQuery, Result<IReadOnlyList<LdapGroupDto>>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public GetGroupParentsQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<IReadOnlyList<LdapGroupDto>>> Handle(GetGroupParentsQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("GetGroupParents", context,
         () => _ldapGatewayClient.GetGroupParentsAsync(context, request.Identifier, request.IdentifierAttribute, cancellationToken),
         cancellationToken);
   }
}
