using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetGroupParents;

public sealed record GetGroupParentsQuery(
    string ServerProfile,
    CatalogType CatalogType,
    string Identifier,
    LdapIdentifierAttribute IdentifierAttribute,
    LdapEntryAttributeSet RequiredAttributeSet,
    bool GroupMustExist);

public sealed class GetGroupParentsQueryValidator : AbstractValidator<GetGroupParentsQuery>
{
    public GetGroupParentsQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.IdentifierAttribute).IsInEnum();
        RuleFor(x => x.RequiredAttributeSet).IsInEnum();
    }
}

/// <summary>Handles <see cref="GetGroupParentsQuery"/>.</summary>
public sealed class GetGroupParentsQueryHandler : DirectoryLdapHandlerBase
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public GetGroupParentsQueryHandler(
        IDirectoryServiceConnector directoryServiceConnector,
        IDomainEventPublisher domainEventPublisher,
        DirectoryCache cache)
        : base(domainEventPublisher, cache)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(GetGroupParentsQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteCachedQueryAsync(
            "GetGroupParents",
            context,
            CacheKeys.GroupParents(request),
            CachePolicies.Parents(),
            cacheNullAsNotFound: false,
            token => _directoryServiceConnector.GetGroupParentsAsync(context, request.Identifier, request.IdentifierAttribute, request.RequiredAttributeSet, request.GroupMustExist, token),
            cancellationToken);
    }
}
