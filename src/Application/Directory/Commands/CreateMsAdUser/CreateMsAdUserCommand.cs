using Bitai.LDAPGateway.Application.Common;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Bitai.LDAPGateway.Application.Directory.Commands.CreateMsAdUser;

public sealed record CreateMsAdUserCommand(
   string ServerProfile,
   CatalogType CatalogType) : CreateMsAdUserDto, IRequest<Result<DirectoryEntryDto>>;
// public sealed record CreateMsAdUserCommand(
//    string ServerProfile,
//    CatalogType CatalogType,
//    string DistinguishedName,
//    string DistinguishedNameOfContainer,
//    string GivenName,
//    string Surname,
//    string CommonName,
//    string Name,
//    string DisplayName,
//    string Description,
//    string[] ObjectClass,
//    string SamAccountName,
//    string UserPrincipalName,
//    string UserAccountControl,
//    string Department,
//    string TelephoneNumber,
//    string Mail,
//    string Password) : IRequest<Result<DirectoryEntryDto>>;

public sealed class CreateMsAdUserCommandValidator : AbstractValidator<CreateMsAdUserCommand>
{
    public CreateMsAdUserCommandValidator()
    {
        RuleFor(x => x.ServerProfile).NotEmpty();
        RuleFor(x => x.CatalogType).NotEqual(CatalogType.LC).WithMessage("Cannot create user accounts in the global catalog of the LDAP server.");

        RuleFor(x => x.DistinguishedName).NotEmpty();
        RuleFor(x => x.DistinguishedNameOfContainer).NotEmpty();
        RuleFor(x => x.Cn).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty();
        RuleFor(x => x.SAMAccountName).NotEmpty();
        RuleFor(x => x.ObjectClass).NotEmpty();
    }
}

public sealed class CreateMsAdUserCommandHandler : LdapHandlerBase, IRequestHandler<CreateMsAdUserCommand, Result<DirectoryEntryDto>>
{
    private readonly ILdapGatewayClient _ldapGatewayClient;

    public CreateMsAdUserCommandHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
       : base(domainEventPublisher)
    {
        _ldapGatewayClient = ldapGatewayClient;
    }

    public Task<Result<DirectoryEntryDto>> Handle(CreateMsAdUserCommand request, CancellationToken cancellationToken)
    {
        var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
        var payload = new CreateMsAdUserDto(
           request.DistinguishedNameOfContainer!,
           request.GivenName,
           request.Sn,
           request.Cn,
           request.Name,
           request.DisplayName,
           request.Description,
           request.DistinguishedName,
           request.ObjectClass,
           request.SAMAccountName,
           request.UserPrincipalName,
           request.UserAccountControl,
           request.Department,
           request.TelephoneNumber,
           request.Mail,
           request.Password);

        return ExecuteAsync("CreateMsAdUser", context,
           () => _ldapGatewayClient.CreateMsAdUserAsync(context, payload, cancellationToken),
           cancellationToken);
    }
}
