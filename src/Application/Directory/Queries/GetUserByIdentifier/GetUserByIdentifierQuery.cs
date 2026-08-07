using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;

public sealed record GetUserByIdentifierQuery(
   string ServerProfile,
   CatalogType CatalogType,
   LdapIdentifierAttribute identifierAttribute,
   string identifier,
   LdapEntryAttributeSet requiredAttributeSet) : IRequest<Result<LdapEntryDto?>>;

public sealed class GetUserByIdentifierQueryValidator : AbstractValidator<GetUserByIdentifierQuery>
{
    public GetUserByIdentifierQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.identifierAttribute).IsInEnum();
        RuleFor(x => x.identifier).NotEmpty();
        RuleFor(x => x.requiredAttributeSet).IsInEnum();
    }
}

public sealed class GetUserByIdentifierQueryHandler : LdapHandlerBase, IRequestHandler<GetUserByIdentifierQuery, Result<LdapEntryDto?>>
{
    private readonly IDirectoryConnector _ldapGatewayClient;

    public GetUserByIdentifierQueryHandler(IDirectoryConnector ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
       : base(domainEventPublisher)
    {
        _ldapGatewayClient = ldapGatewayClient;
    }

    public Task<Result<LdapEntryDto?>> Handle(GetUserByIdentifierQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync("GetUserByIdentifier", context,
           () => _ldapGatewayClient.GetUserAsync(context, request.identifierAttribute, request.identifier, request.requiredAttributeSet, cancellationToken),
           cancellationToken);
    }
}
