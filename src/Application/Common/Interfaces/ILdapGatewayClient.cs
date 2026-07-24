using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Application.Common.Interfaces;

public interface ILdapGatewayClient
{
    #region Authentication Methods
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
    #endregion

    #region User Provisioning Methods
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
        IdentifierAttribute identifierAttribute,
        string identifier,
        string? reason,
        CancellationToken cancellationToken);

    Task<Result> DeleteMsAdUserAsync(
        LdapRequestContext context,
        IdentifierAttribute identifierAttribute,
        string identifier,
        CancellationToken cancellationToken);
    #endregion

    #region Generic Directory Methods
    Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(
        LdapRequestContext context,
        IdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(
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
    #endregion

    #region Group Directory Methods
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
    #endregion
}
