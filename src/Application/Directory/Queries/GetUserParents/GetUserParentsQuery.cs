using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetUserParents;

public sealed record GetUserParentsQuery(
   string ServerProfile,
   CatalogType CatalogType,
   string Identifier,
   string IdentifierAttribute) : IRequest<Result<IReadOnlyList<DirectoryEntryDto>>>;

public sealed class GetUserParentsQueryValidator : AbstractValidator<GetUserParentsQuery>
{
   public GetUserParentsQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
      RuleFor(x => x.IdentifierAttribute).NotEmpty();
   }
}

public sealed class GetUserParentsQueryHandler : LdapHandlerBase, IRequestHandler<GetUserParentsQuery, Result<IReadOnlyList<DirectoryEntryDto>>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public GetUserParentsQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<IReadOnlyList<DirectoryEntryDto>>> Handle(GetUserParentsQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("GetUserParents", context,
         () => _ldapGatewayClient.GetUserParentsAsync(context, request.Identifier, request.IdentifierAttribute, cancellationToken),
         cancellationToken);
   }
}
