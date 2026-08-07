using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Authentications.Commands.Authenticate;

public sealed record AuthenticateCommand(
    string ServerProfile,
    CatalogType CatalogType,
    UserCredential credential) : IRequest<Result<AuthenticationResultDto>>;

public sealed class AuthenticateCommandValidator : AbstractValidator<AuthenticateCommand>
{
    public AuthenticateCommandValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).IsInEnum();
        RuleFor(x => x.credential.Username).NotEmpty();
        RuleFor(x => x.credential.Password).NotNull();
    }
}

public sealed class AuthenticateCommandHandler : LdapHandlerBase, IRequestHandler<AuthenticateCommand, Result<AuthenticationResultDto>>
{
    private readonly ILdapGatewayClient _ldapGatewayClient;

    public AuthenticateCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
        : base(domainEventPublisher)
    {
        _ldapGatewayClient = ldapGatewayClient;
    }

    public Task<Result<AuthenticationResultDto>> Handle(AuthenticateCommand request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);

        return ExecuteAsync(
            operationName: "Authenticate",
            context,
            () => _ldapGatewayClient.AuthenticateAsync(context, request.credential, cancellationToken),
            cancellationToken);
    }
}
