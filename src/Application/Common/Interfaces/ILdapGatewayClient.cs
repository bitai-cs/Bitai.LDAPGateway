using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Application.Common.Interfaces;

public interface ILdapGatewayClient
{
    Task<Result<AuthenticationResultDto>> AuthenticateAsync(
       LdapRequestContext context,
       string username,
       string password,
       CancellationToken cancellationToken);

    Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(
      LdapRequestContext context,
      string username,
      string password,
      CancellationToken cancellationToken);

    Task<Result<DirectoryEntryDto>> CreateMsAdUserAsync(
       LdapRequestContext context,
       CreateMsAdUserDto request,
       CancellationToken cancellationToken);

    Task<Result> SetMsAdUserPasswordAsync(
       LdapRequestContext context,
       IdentifierAttribute identifierAttribute,
       string identifier,
       string newPassword,
       bool mustChangeAtNextLogon,
       CancellationToken cancellationToken);

    Task<Result> DisableMsAdUserAsync(
       LdapRequestContext context,
       string identifier,
       string? reason,
       CancellationToken cancellationToken);

    Task<Result> DeleteMsAdUserAsync(
       LdapRequestContext context,
       string identifier,
       CancellationToken cancellationToken);

    Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(
       LdapRequestContext context,
       string identifier,
       string identifierAttribute,
       CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(
       LdapRequestContext context,
       string filter,
       int sizeLimit,
       CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DirectoryEntryDto>>> GetUserParentsAsync(
       LdapRequestContext context,
       string identifier,
       string identifierAttribute,
       CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapUserDto>>> SearchUsersAsync(
       LdapRequestContext context,
       string filter,
       int sizeLimit,
       CancellationToken cancellationToken);

    Task<Result<LdapGroupDto>> GetGroupAsync(
       LdapRequestContext context,
       string identifier,
       string identifierAttribute,
       CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapGroupDto>>> GetGroupParentsAsync(
       LdapRequestContext context,
       string identifier,
       string identifierAttribute,
       CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapGroupDto>>> SearchGroupsAsync(
       LdapRequestContext context,
       string filter,
       int sizeLimit,
       CancellationToken cancellationToken);
}
