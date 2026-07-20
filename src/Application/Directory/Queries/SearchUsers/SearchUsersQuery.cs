using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.SearchUsers;

public sealed record SearchUsersQuery(
   string ServerProfile,
   CatalogType CatalogType,
   string Filter,
   int SizeLimit) : IRequest<Result<IReadOnlyList<LdapUserDto>>>;

public sealed class SearchUsersQueryValidator : AbstractValidator<SearchUsersQuery>
{
   public SearchUsersQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Filter).NotEmpty();
      RuleFor(x => x.SizeLimit).GreaterThan(0).LessThanOrEqualTo(2000);
   }
}

public sealed class SearchUsersQueryHandler : LdapHandlerBase, IRequestHandler<SearchUsersQuery, Result<IReadOnlyList<LdapUserDto>>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public SearchUsersQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<IReadOnlyList<LdapUserDto>>> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("SearchUsers", context,
         () => _ldapGatewayClient.SearchUsersAsync(context, request.Filter, request.SizeLimit, cancellationToken),
         cancellationToken);
   }
}
