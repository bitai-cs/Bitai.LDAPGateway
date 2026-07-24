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
   IdentifierAttribute IdentifierAttribute,
   LdapEntryAttributeSet RequiredAttributeSet) : IRequest<Result<DirectoryEntryDto>>;

public sealed class GetDirectoryEntryByIdentifierQueryValidator : AbstractValidator<GetDirectoryEntryByIdentifierQuery>
{
   public GetDirectoryEntryByIdentifierQueryValidator()
   {
      RuleFor(x => x.ServerProfile).NotEmpty();
      RuleFor(x => x.Identifier).NotEmpty();
      RuleFor(x => x.IdentifierAttribute).IsInEnum();
      RuleFor(x => x.RequiredAttributeSet).IsInEnum();
   }
}

public sealed class GetDirectoryEntryByIdentifierQueryHandler : LdapHandlerBase, IRequestHandler<GetDirectoryEntryByIdentifierQuery, Result<DirectoryEntryDto>>
{
   private readonly ILdapGatewayClient _ldapGatewayClient;

   public GetDirectoryEntryByIdentifierQueryHandler(ILdapGatewayClient ldapGatewayClient, IDomainEventPublisher domainEventPublisher)
      : base(domainEventPublisher)
   {
      _ldapGatewayClient = ldapGatewayClient;
   }

   public Task<Result<DirectoryEntryDto>> Handle(GetDirectoryEntryByIdentifierQuery request, CancellationToken cancellationToken)
   {
      var context = new LdapRequestContext(request.ServerProfile, request.CatalogType);
      return ExecuteAsync("GetDirectoryEntryByIdentifier", context,
         () => _ldapGatewayClient.GetDirectoryEntryAsync(context, request.IdentifierAttribute, request.Identifier, request.RequiredAttributeSet, cancellationToken),
         cancellationToken);
   }
}
