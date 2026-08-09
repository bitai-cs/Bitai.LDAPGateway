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
    LdapEntryAttribute FilterAttribute, string FilterValue,
    LdapEntryAttribute? SecondFilterAttribute, string? SecondFilterValue,
    bool? CombineFilters,
    LdapEntryAttributeSet RequiredAttributeSet,
    int SizeLimit) : IRequest<Result<IReadOnlyList<LdapEntryDto>>>;

public sealed class SearchGroupsQueryValidator : AbstractValidator<SearchGroupsQuery>
{
    public SearchGroupsQueryValidator()
    {
        RuleFor(x => x.ServerProfile)
            .NotEmpty().WithMessage("Server profile is required.");

        RuleFor(x => x.FilterValue)
            .NotEmpty().WithMessage("Filter value is required.");

        // Enum validations (Prevents invalid integer casts from external input)
        RuleFor(x => x.CatalogType)
            .IsInEnum().WithMessage("Invalid Catalog type value.");

        RuleFor(x => x.FilterAttribute)
            .IsInEnum().WithMessage("Invalid Filter attribute value.");

        RuleFor(x => x.RequiredAttributeSet)
            .IsInEnum().WithMessage("Invalid Required attribute set value.");

        // Secondary Filter Conditional Logic (All-or-nothing approach)
        RuleFor(x => x.SecondFilterValue)
            .NotEmpty().WithMessage("Second filter value is required when Second filter attribute is provided.")
            .When(x => x.SecondFilterAttribute.HasValue);

        RuleFor(x => x.CombineFilters)
            .NotNull().WithMessage("Combine filters is required when Second filter attribute is provided.")
            .When(x => x.SecondFilterAttribute.HasValue);

        RuleFor(x => x.SecondFilterValue)
            .Null().WithMessage("Second filter value must be null when Second filter attribute is not provided.")
            .When(x => !x.SecondFilterAttribute.HasValue);

        RuleFor(x => x.CombineFilters)
            .Null().WithMessage("Combine filters must be null when Second filter attribute is not provided.")
            .When(x => !x.SecondFilterAttribute.HasValue);

        // Bounds checking
        RuleFor(x => x.SizeLimit)
            .GreaterThan(0).WithMessage("Size limit must be greater than 0.")
            .LessThanOrEqualTo(2000).WithMessage("Size limit cannot exceed 2000.");
    }
}

public sealed class SearchGroupsQueryHandler : LdapHandlerBase, IRequestHandler<SearchGroupsQuery, Result<IReadOnlyList<LdapEntryDto>>>
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public SearchGroupsQueryHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(SearchGroupsQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
        return ExecuteAsync("SearchGroups", context,
            () => _directoryServiceConnector.SearchGroupsAsync(context, request.FilterAttribute, request.FilterValue, request.SecondFilterAttribute, request.SecondFilterValue, request.CombineFilters, request.RequiredAttributeSet, request.SizeLimit, cancellationToken),
        cancellationToken);
    }
}
