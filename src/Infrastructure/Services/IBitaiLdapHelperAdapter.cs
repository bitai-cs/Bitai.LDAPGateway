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
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        string username,
        string password,
        CancellationToken cancellationToken);

    Task<Result<DirectoryEntryDto>> CreateMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        CreateMsAdUserDto user,
        CancellationToken cancellationToken);

    Task<Result> SetMsAdUserPasswordAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        IdentifierAttribute identifierAttribute,
        string identifier,
        string password,
        bool mustChangeAtNextLogon,
        CancellationToken cancellationToken);

    Task<Result> DisableMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        IdentifierAttribute identifierAttribute,
        string identifier,
        string? reason,
        CancellationToken cancellationToken);

    Task<Result> DeleteMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        IdentifierAttribute identifierAttribute,
        string identifier,
        CancellationToken cancellationToken);

    Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        IdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute FilterAttribute,
        string FilterValue,
        LdapEntryAttribute? SecondFilterAttribute,
        string? SecondFilterValue,
        bool? CombineFilters,
        int sizeLimit,
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
