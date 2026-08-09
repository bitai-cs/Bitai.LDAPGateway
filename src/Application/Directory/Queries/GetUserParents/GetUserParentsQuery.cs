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
    LdapIdentifierAttribute IdentifierAttribute,
    LdapEntryAttributeSet RequiredAttributeSet,
    bool UserMustExist) : IRequest<Result<IReadOnlyList<LdapEntryDto>>>;

public sealed class GetUserParentsQueryValidator : AbstractValidator<GetUserParentsQuery>
{
    public GetUserParentsQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.IdentifierAttribute).IsInEnum();
        RuleFor(x => x.RequiredAttributeSet).IsInEnum();
    }
}

public sealed class GetUserParentsQueryHandler : LdapHandlerBase, IRequestHandler<GetUserParentsQuery, Result<IReadOnlyList<LdapEntryDto>>>
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public GetUserParentsQueryHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(GetUserParentsQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync("GetUserParents", context,
            () => _directoryServiceConnector.GetUserParentsAsync(context, request.Identifier, request.IdentifierAttribute, request.RequiredAttributeSet, request.UserMustExist, cancellationToken),
            cancellationToken);
    }
}
