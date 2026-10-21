using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Directory.Queries.SearchDirectory;

public sealed record SearchDirectoryQuery(
   string ServerProfile,
   CatalogType CatalogType,
   LdapEntryAttribute FilterAttribute,
   string FilterValue,
   LdapEntryAttribute? SecondFilterAttribute,
   string? SecondFilterValue,
   bool? CombineFilters,
   LdapEntryAttributeSet RequiredAttributeSet);

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
        RuleFor(x => x.RequiredAttributeSet).IsInEnum();
    }
}

public sealed class SearchDirectoryQueryHandler : LdapHandlerBase
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public SearchDirectoryQueryHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
       : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(SearchDirectoryQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
        return ExecuteAsync("SearchDirectory", context,
           () => _directoryServiceConnector.SearchDirectoryAsync(context, request.FilterAttribute, request.FilterValue, request.SecondFilterAttribute, request.SecondFilterValue, request.CombineFilters, request.RequiredAttributeSet, cancellationToken),
           cancellationToken);
    }
}
