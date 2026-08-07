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
    LdapEntryAttributeSet RequiredAttributeSet) : IRequest<Result<IReadOnlyList<LdapEntryDto>>>;

public sealed class GetUserParentsQueryValidator : AbstractValidator<GetUserParentsQuery>
{
    public GetUserParentsQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.IdentifierAttribute).IsInEnum();
        RuleFor(x => x.RequiredAttributeSet).IsInEnum();
    }
}

public sealed class GetUserParentsQueryHandler : LdapHandlerBase, IRequestHandler<GetUserParentsQuery, Result<IReadOnlyList<LdapEntryDto>>>
{
    private readonly IDirectoryConnector _ldapGatewayClient;

    public GetUserParentsQueryHandler(IDirectoryConnector ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _ldapGatewayClient = ldapGatewayClient;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(GetUserParentsQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync("GetUserParents", context,
            () => _ldapGatewayClient.GetUserParentsAsync(context, request.Identifier, request.IdentifierAttribute, request.RequiredAttributeSet, cancellationToken),
            cancellationToken);
    }
}
