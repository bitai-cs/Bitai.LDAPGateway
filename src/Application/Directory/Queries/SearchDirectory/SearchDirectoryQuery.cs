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
   LdapEntryAttribute FilterAttribute,
   string FilterValue,
   LdapEntryAttribute? SecondFilterAttribute,
   string? SecondFilterValue,
   bool? CombineFilters,
   int SizeLimit) : IRequest<Result<IReadOnlyList<DirectoryEntryDto>>>;

public sealed class SearchDirectoryQueryValidator : AbstractValidator<SearchDirectoryQuery>
{
    public SearchDirectoryQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.FilterAttribute).NotEmpty();
        RuleFor(x => x.FilterValue).NotEmpty();
        RuleFor(x => x.SecondFilterAttribute).NotEmpty().When(x => x.SecondFilterValue != null);
        RuleFor(x => x.SecondFilterValue).NotEmpty().When(x => x.SecondFilterAttribute != null);
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
           () => _ldapGatewayClient.SearchDirectoryAsync(context, request.FilterAttribute, request.FilterValue, request.SecondFilterAttribute, request.SecondFilterValue, request.CombineFilters, request.SizeLimit, cancellationToken),
           cancellationToken);
    }
}
