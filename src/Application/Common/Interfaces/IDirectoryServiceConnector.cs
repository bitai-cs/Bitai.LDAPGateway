using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;

namespace Bitai.LDAPGateway.Application.Common.Interfaces;

public interface IDirectoryServiceConnector
{
    #region Authentication Methods
    Task<Result<AuthenticationResultDto>> AuthenticateAsync(
        LdapRequestContext context,
        UserCredential credential,
        CancellationToken cancellationToken);

    Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(
        LdapRequestContext context,
        UserCredential credential,
        CancellationToken cancellationToken);
    #endregion

    #region User Provisioning Methods
    Task<Result<LdapEntryDto>> CreateMsAdUserAsync(
        LdapRequestContext context,
        CreateMsAdUserDto request,
        CancellationToken cancellationToken);

    Task<Result> SetMsAdUserPasswordAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        string newPassword,
        bool mustChangeAtNextLogon,
        CancellationToken cancellationToken);

    Task<Result> DisableMsAdUserAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        string? reason,
        CancellationToken cancellationToken);

    Task<Result> DeleteMsAdUserAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        CancellationToken cancellationToken);
    #endregion

    #region Generic Directory Methods
    Task<Result<LdapEntryDto?>> GetDirectoryEntryAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> SearchDirectoryAsync(
        LdapRequestContext context,
        LdapEntryAttribute FilterAttribute,
        string FilterValue,
        LdapEntryAttribute? SecondFilterAttribute,
        string? SecondFilterValue,
        bool? CombineFilters,
        int sizeLimit,
        CancellationToken cancellationToken);
    #endregion

    #region User Directory Methods
    Task<Result<LdapEntryDto?>> GetUserAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> GetUserParentsAsync(
        LdapRequestContext context,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        bool userMustExist,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> SearchUsersAsync(
        LdapRequestContext context,
        LdapEntryAttribute filterAttribute, string filterValue,
        LdapEntryAttribute? secondFilterAttribute, string? secondFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        int sizeLimit,
        CancellationToken cancellationToken);
    #endregion

    #region Group Directory Methods
    Task<Result<LdapEntryDto?>> GetGroupAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> GetGroupParentsAsync(
        LdapRequestContext context,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<LdapEntryDto>>> SearchGroupsAsync(
        LdapRequestContext context,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        int sizeLimit,
        CancellationToken cancellationToken);
    #endregion
}
