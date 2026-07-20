using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.SearchDirectory;

public sealed record SearchDirectoryQuery(
   string ServerProfile,
   CatalogType CatalogType,
   string Filter,
   int SizeLimit) : IRequest<Result<IReadOnlyList<DirectoryEntryDto>>>;

public sealed class SearchDirectoryQueryValidator : AbstractValidator<SearchDirectoryQuery>
{
   public SearchDirectoryQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Filter).NotEmpty();
      RuleFor(x => x.SizeLimit).GreaterThan(0).LessThanOrEqualTo(2000);
   }
}

public sealed class SearchDirectoryQueryHandler : LdapHandlerBase, IRequestHandler<SearchDirectoryQuery, Result<IReadOnlyList<DirectoryEntryDto>>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public SearchDirectoryQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<IReadOnlyList<DirectoryEntryDto>>> Handle(SearchDirectoryQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("SearchDirectory", context,
         () => _ldapGatewayClient.SearchDirectoryAsync(context, request.Filter, request.SizeLimit, cancellationToken),
         cancellationToken);
   }
}
