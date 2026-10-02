using Bitai.LDAPGateway.Application.Common;
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

public sealed class GetGroupParentsQueryHandler : LdapHandlerBase
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public GetGroupParentsQueryHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(GetGroupParentsQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync("GetGroupParents", context,
            () => _directoryServiceConnector.GetGroupParentsAsync(context, request.Identifier, request.IdentifierAttribute, request.RequiredAttributeSet, request.GroupMustExist, cancellationToken),
            cancellationToken);
    }
}
