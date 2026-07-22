using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Infrastructure.Options;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public interface IBitaiLdapHelperAdapter
{
   Task<Result<AuthenticationResultDto>> AuthenticateAsync(
      LdapServerProfileOption ldapServerProfile,
      CatalogType catalogType,
      string username,
      string password,
      CancellationToken cancellationToken);

    Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(
      string server,
      CatalogType catalogType,
      string username,
      string password,
      CancellationToken cancellationToken);

    Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      string identifierAttribute,
      CancellationToken cancellationToken);

   Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(
      string server,
      CatalogType catalogType,
      string filter,
      int sizeLimit,
      CancellationToken cancellationToken);

   Task<Result<DirectoryEntryDto>> CreateMsAdUserAsync(
      string server,
      CatalogType catalogType,
      CreateMsAdUserDto user,
      CancellationToken cancellationToken);

   Task<Result> SetMsAdUserPasswordAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      string password,
      bool mustChangeAtNextLogon,
      CancellationToken cancellationToken);

   Task<Result> DisableMsAdUserAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      string? reason,
      CancellationToken cancellationToken);

   Task<Result> DeleteMsAdUserAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      CancellationToken cancellationToken);

   Task<Result<IReadOnlyList<DirectoryEntryDto>>> GetUserParentsAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      string identifierAttribute,
      CancellationToken cancellationToken);

   Task<Result<IReadOnlyList<LdapUserDto>>> SearchUsersAsync(
      string server,
      CatalogType catalogType,
      string filter,
      int sizeLimit,
      CancellationToken cancellationToken);

   Task<Result<LdapGroupDto>> GetGroupAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      string identifierAttribute,
      CancellationToken cancellationToken);

   Task<Result<IReadOnlyList<LdapGroupDto>>> GetGroupParentsAsync(
      string server,
      CatalogType catalogType,
      string identifier,
      string identifierAttribute,
      CancellationToken cancellationToken);

   Task<Result<IReadOnlyList<LdapGroupDto>>> SearchGroupsAsync(
      string server,
      CatalogType catalogType,
      string filter,
      int sizeLimit,
      CancellationToken cancellationToken);
}
