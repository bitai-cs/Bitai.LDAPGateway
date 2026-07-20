using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.SearchGroups;

public sealed record SearchGroupsQuery(
   string ServerProfile,
   CatalogType CatalogType,
   string Filter,
   int SizeLimit) : IRequest<Result<IReadOnlyList<LdapGroupDto>>>;

public sealed class SearchGroupsQueryValidator : AbstractValidator<SearchGroupsQuery>
{
   public SearchGroupsQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Filter).NotEmpty();
      RuleFor(x => x.SizeLimit).GreaterThan(0).LessThanOrEqualTo(2000);
   }
}

public sealed class SearchGroupsQueryHandler : LdapHandlerBase, IRequestHandler<SearchGroupsQuery, Result<IReadOnlyList<LdapGroupDto>>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public SearchGroupsQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<IReadOnlyList<LdapGroupDto>>> Handle(SearchGroupsQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("SearchGroups", context,
         () => _ldapGatewayClient.SearchGroupsAsync(context, request.Filter, request.SizeLimit, cancellationToken),
         cancellationToken);
   }
}
