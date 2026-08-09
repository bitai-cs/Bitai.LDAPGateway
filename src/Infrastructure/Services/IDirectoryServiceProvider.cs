using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Infrastructure.Options;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public interface IDirectoryServiceProvider
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

    Task<Result<LdapEntryDto>> CreateMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        CreateMsAdUserDto user,
        CancellationToken cancellationToken);

    Task<Result> SetMsAdUserPasswordAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        string password,
        bool mustChangeAtNextLogon,
        CancellationToken cancellationToken);

    Task<Result> DisableMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        string? reason,
        CancellationToken cancellationToken);

    Task<Result> DeleteMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        CancellationToken cancellationToken);

    Task<Result<LdapEntryDto?>> GetDirectoryEntryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> SearchDirectoryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute FilterAttribute,
        string FilterValue,
        LdapEntryAttribute? SecondFilterAttribute,
        string? SecondFilterValue,
        bool? CombineFilters,
        int sizeLimit,
        CancellationToken cancellationToken);

    Task<Result<LdapEntryDto?>> GetUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> GetUserParentsAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        bool userMustExist,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> SearchUsersAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        int sizeLimit,
        CancellationToken cancellationToken);

    Task<Result<LdapEntryDto?>> GetGroupAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> GetGroupParentsAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> SearchGroupsAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        int sizeLimit,
        CancellationToken cancellationToken);
}
