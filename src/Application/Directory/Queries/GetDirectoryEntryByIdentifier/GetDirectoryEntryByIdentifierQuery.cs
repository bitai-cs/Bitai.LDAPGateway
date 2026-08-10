using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Queries.GetDirectoryEntryByIdentifier;

public sealed record GetDirectoryEntryByIdentifierQuery(
    string ServerProfile,
    CatalogType CatalogType,
    string Identifier,
    LdapIdentifierAttribute IdentifierAttribute,
    LdapEntryAttributeSet RequiredAttributeSet,
    bool UserMustExists) : IRequest<Result<LdapEntryDto?>>;

public sealed class GetDirectoryEntryByIdentifierQueryValidator : AbstractValidator<GetDirectoryEntryByIdentifierQuery>
{
    public GetDirectoryEntryByIdentifierQueryValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.Identifier).NotEmpty();
        RuleFor(x => x.IdentifierAttribute).IsInEnum();
        RuleFor(x => x.RequiredAttributeSet).IsInEnum();
    }
}

public sealed class GetDirectoryEntryByIdentifierQueryHandler : LdapHandlerBase, IRequestHandler<GetDirectoryEntryByIdentifierQuery, Result<LdapEntryDto?>>
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public GetDirectoryEntryByIdentifierQueryHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public async Task<Result<LdapEntryDto?>> Handle(GetDirectoryEntryByIdentifierQuery request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        var result = await ExecuteAsync("GetDirectoryEntryByIdentifier", context,
            () => _directoryServiceConnector.GetDirectoryEntryAsync(context, request.IdentifierAttribute, request.Identifier, request.RequiredAttributeSet, cancellationToken),
            cancellationToken);

        // If the result is not successful or if it is successful and the value is not null, return the result.
        if (!result.IsSuccess || (result.IsSuccess && result.Value != null))
        {
            return result;
        }

        if (request.UserMustExists)
            return Result<LdapEntryDto?>.Failure(Error.NotFound($"Entry not found in the catalog for {request.IdentifierAttribute}={request.Identifier}"));
        else
            return result;
    }
}
