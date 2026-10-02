using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;
using FluentValidation;

namespace Bitai.LDAPGateway.Application.Authentications.Commands.AuthenticateWithoutUserLookup;

public sealed record AuthenticateWithoutUserLookupCommand(
    string ServerProfile,
    CatalogType CatalogType,
    UserCredential Credential);

public sealed class AuthenticateWithoutUserLookupCommandValidator : AbstractValidator<AuthenticateWithoutUserLookupCommand>
{
    public AuthenticateWithoutUserLookupCommandValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.Credential).NotNull();
        RuleFor(x => x.Credential.Username).NotEmpty();
        RuleFor(x => x.Credential.Password).NotNull();
    }
}

public sealed class AuthenticateWithoutUserLookupCommandHandler : LdapHandlerBase
{
    private readonly IDirectoryServiceConnector _directoryServiceConnector;

    public AuthenticateWithoutUserLookupCommandHandler(IDirectoryServiceConnector directoryServiceConnector, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _directoryServiceConnector = directoryServiceConnector;
    }

    public Task<Result<AuthenticationResultDto>> Handle(AuthenticateWithoutUserLookupCommand request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync(
            operationName: "AuthenticateWithoutUserLookup",
            context,
            () => _directoryServiceConnector.AuthenticateWithoutUserLookupAsync(context, request.Credential, cancellationToken),
            cancellationToken);
    }
}
