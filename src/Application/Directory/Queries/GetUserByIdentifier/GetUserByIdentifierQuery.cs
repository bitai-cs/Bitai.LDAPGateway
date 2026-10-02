using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetUserByIdentifier;

public sealed record GetUserByIdentifierQuery(
   string ServerProfile,
   CatalogType CatalogType,
   LdapIdentifierAttribute IdentifierAttribute,
   string Identifier,
   LdapEntryAttributeSet RequiredAttributeSet,
   bool UserMustExists);

public sealed class GetUserByIdentifierQueryValidator : AbstractValidator<GetUserByIdentifierQuery>
{
    public GetUserByIdentifierQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.IdentifierAttribute).IsInEnum();
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.RequiredAttributeSet).IsInEnum();
    }
}

public sealed class GetUserByIdentifierQueryHandler : LdapHandlerBase
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public GetUserByIdentifierQueryHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
       : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public async Task<Result<LdapEntryDto?>> Handle(GetUserByIdentifierQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        var result = await ExecuteAsync("GetUserByIdentifier", context,
           () => _directoryServiceConnector.GetUserAsync(context, request.IdentifierAttribute, request.Identifier, request.RequiredAttributeSet, cancellationToken),
           cancellationToken);

        if (!result.IsSuccess || (result.IsSuccess && result.Value != null))
        {
            return result;
        }

        if (request.UserMustExists)
            return Result<LdapEntryDto?>.Failure(Error.NotFound($"User not found in the catalog for {request.IdentifierAttribute}={request.Identifier}"));
        else
            return result;
    }
}
