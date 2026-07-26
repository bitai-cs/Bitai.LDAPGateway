using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetGroupByIdentifier;

public sealed record GetGroupByIdentifierQuery(
   string ServerProfile,
   CatalogType CatalogType,
   LdapIdentifierAttribute identifierAttribute,
   string identifier,
   LdapEntryAttributeSet requiredAttributeSet) : IRequest<Result<LdapEntryDto>>;

public sealed class GetGroupByIdentifierQueryValidator : AbstractValidator<GetGroupByIdentifierQuery>
{
    public GetGroupByIdentifierQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.identifierAttribute).IsInEnum();
        RuleFor(x => x.identifier).NotEmpty();
        RuleFor(x => x.requiredAttributeSet).IsInEnum();
    }
}

public sealed class GetGroupByIdentifierQueryHandler : LdapHandlerBase, IRequestHandler<GetGroupByIdentifierQuery, Result<LdapEntryDto>>
{
    private readonly ILdapGatewayClient _ldapGatewayClient;

    public GetGroupByIdentifierQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
       : base(domainEventPublisher)
    {
        _ldapGatewayClient = ldapGatewayClient;
    }

    public Task<Result<LdapEntryDto>> Handle(GetGroupByIdentifierQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
        return ExecuteAsync("GetGroupByIdentifier", context,
           () => _ldapGatewayClient.GetGroupAsync(context, request.identifierAttribute, request.identifier, request.requiredAttributeSet, cancellationToken),
           cancellationToken);
    }
}
