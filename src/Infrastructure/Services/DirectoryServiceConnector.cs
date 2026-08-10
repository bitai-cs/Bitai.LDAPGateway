using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.ValueObjects;
using Bitai.LDAPGateway.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class DirectoryServiceConnector : IDirectoryServiceConnector
{
    private readonly IOptionsMonitor<LdapServerProfilesOptions> _options;
    private readonly IDirectoryServiceProvider _directoryServiceProvider;



    public DirectoryServiceConnector(IOptionsMonitor<LdapServerProfilesOptions> options, IDirectoryServiceProvider adapter)
    {
        _options = options;
        _directoryServiceProvider = adapter;
    }



    #region Authentication Methods
    public async Task<Result<AuthenticationResultDto>> AuthenticateAsync(LdapRequestContext context, UserCredential credential, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<AuthenticationResultDto>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.AuthenticateAsync(profileResult.Value!, context.CatalogType, credential.Username, credential.Password.DangerousGetSecret(), cancellationToken);
    }

    public async Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(LdapRequestContext context, UserCredential credential, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<AuthenticationResultDto>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.AuthenticateWithoutUserLookupAsync(profileResult.Value!, context.CatalogType, credential.Username, credential.Password.DangerousGetSecret(), cancellationToken);
    }
    #endregion


    #region User Provisioning Methods
    public async Task<Result<LdapEntryDto>> CreateMsAdUserAsync(LdapRequestContext context, CreateMsAdUserDto request, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<LdapEntryDto>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.CreateMsAdUserAsync(profileResult.Value!, context.CatalogType, request, cancellationToken);
    }

    public async Task<Result> SetMsAdUserPasswordAsync(LdapRequestContext context, LdapIdentifierAttribute identifierAttribute, string identifier, string newPassword, bool mustChangeAtNextLogon, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.SetMsAdUserPasswordAsync(profileResult.Value!, context.CatalogType, identifierAttribute, identifier, newPassword, mustChangeAtNextLogon, cancellationToken);
    }

    public async Task<Result> DisableMsAdUserAsync(LdapRequestContext context, LdapIdentifierAttribute identifierAttribute, string identifier, string? reason, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.DisableMsAdUserAsync(profileResult.Value!, context.CatalogType, identifierAttribute, identifier, reason, cancellationToken);
    }

    public async Task<Result> DeleteMsAdUserAsync(LdapRequestContext context, LdapIdentifierAttribute identifierAttribute, string identifier, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.DeleteMsAdUserAsync(profileResult.Value!, context.CatalogType, identifierAttribute, identifier, cancellationToken);
    }
    #endregion


    #region Generic Directory Search Methods
    public async Task<Result<LdapEntryDto?>> GetDirectoryEntryAsync(LdapRequestContext context, LdapIdentifierAttribute identifierAttribute, string identifier, LdapEntryAttributeSet requiredAttributeSet, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<LdapEntryDto?>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.GetDirectoryEntryAsync(profileResult.Value!, context.CatalogType, identifierAttribute, identifier, requiredAttributeSet, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> SearchDirectoryAsync(LdapRequestContext context, LdapEntryAttribute FilterAttribute, string FilterValue, LdapEntryAttribute? SecondFilterAttribute, string? SecondFilterValue, bool? CombineFilters, int sizeLimit, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.SearchDirectoryAsync(profileResult.Value!, context.CatalogType, FilterAttribute, FilterValue, SecondFilterAttribute, SecondFilterValue, CombineFilters, sizeLimit, cancellationToken);
    }
    #endregion


    #region User Search Methods
    public async Task<Result<LdapEntryDto?>> GetUserAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<LdapEntryDto?>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.GetUserAsync(profileResult.Value!, context.CatalogType, identifierAttribute, identifier, requiredAttributeSet, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> GetUserParentsAsync(LdapRequestContext context, string identifier, LdapIdentifierAttribute identifierAttribute, LdapEntryAttributeSet requiredAttributeSet, bool userMustExist, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.GetUserParentsAsync(profileResult.Value!, context.CatalogType, identifier, identifierAttribute, requiredAttributeSet, userMustExist, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> SearchUsersAsync(LdapRequestContext context, LdapEntryAttribute filterAttribute, string filterValue, LdapEntryAttribute? secondFilterAttribute, string? secondFilterValue, bool? combineFilters, LdapEntryAttributeSet requiredAttributeSet, int sizeLimit, CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.SearchUsersAsync(profileResult.Value!, context.CatalogType, filterAttribute, filterValue, secondFilterAttribute, secondFilterValue, combineFilters, requiredAttributeSet, sizeLimit, cancellationToken);
    }
    #endregion


    #region Group Search Methods
    public async Task<Result<LdapEntryDto?>> GetGroupAsync(
        LdapRequestContext context,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<LdapEntryDto?>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.GetGroupAsync(profileResult.Value!, context.CatalogType, identifierAttribute, identifier, requiredAttributeSet, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> GetGroupParentsAsync(
        LdapRequestContext context,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        bool groupMustExist,
        CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.GetGroupParentsAsync(profileResult.Value!, context.CatalogType, identifier, identifierAttribute, requiredAttributeSet, groupMustExist, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> SearchGroupsAsync(
        LdapRequestContext context,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        int sizeLimit,
        CancellationToken cancellationToken)
    {
        var profileResult = GetLdapServerProfileConfiguration(context.ServerProfile);
        if (!profileResult.IsSuccess)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(profileResult.Error!);
        }

        return await _directoryServiceProvider.SearchGroupsAsync(profileResult.Value!, context.CatalogType, filterAttribute, filterValue, secondaryFilterAttribute, secondaryFilterValue, combineFilters, requiredAttributeSet, sizeLimit, cancellationToken);
    }
    #endregion


    #region Private Methods   
    private Result<LdapServerProfileOption> GetLdapServerProfileConfiguration(string profileId)
    {
        var profile = _options.CurrentValue
            .SingleOrDefault(x => string.Equals(x.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

        return profile is null
            ? Result<LdapServerProfileOption>.Failure(Error.NotFound($"LDAP server profile '{profileId}' was not found."))
            : Result<LdapServerProfileOption>.Success(profile);
    }
    #endregion
}
