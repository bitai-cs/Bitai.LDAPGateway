using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetGroupParents;

public sealed record GetGroupParentsQuery(
    string ServerProfile,
    CatalogType CatalogType,
    string Identifier,
    LdapIdentifierAttribute IdentifierAttribute,
    LdapEntryAttributeSet RequiredAttributeSet) : IRequest<Result<IReadOnlyList<LdapEntryDto>>>;

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

public sealed class GetGroupParentsQueryHandler : LdapHandlerBase, IRequestHandler<GetGroupParentsQuery, Result<IReadOnlyList<LdapEntryDto>>>
{
    private readonly IDirectoryConnector _ldapGatewayClient;

    public GetGroupParentsQueryHandler(IDirectoryConnector ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _ldapGatewayClient = ldapGatewayClient;
    }

    public Task<Result<IReadOnlyList<LdapEntryDto>>> Handle(GetGroupParentsQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync("GetGroupParents", context,
            () => _ldapGatewayClient.GetGroupParentsAsync(context, request.Identifier, request.IdentifierAttribute, request.RequiredAttributeSet, cancellationToken),
            cancellationToken);
    }
}
