using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Infrastructure.Options;
using Bitai.LDAPHelper;
using Bitai.LDAPHelper.DTO;
using Bitai.LDAPHelper.LdapAdapters.Novell;
using Bitai.LDAPHelper.QueryFilters;
using Microsoft.Extensions.Logging;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class BitaiLdapHelperNovellAdapter : IBitaiLdapHelperAdapter
{
    private readonly ILogger<BitaiLdapHelperNovellAdapter> _logger;



    public BitaiLdapHelperNovellAdapter(ILogger<BitaiLdapHelperNovellAdapter> logger)
    {
        _logger = logger;
    }



    #region Authentication Methods
    public async Task<Result<AuthenticationResultDto>> AuthenticateAsync(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("Username is required."));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("Password is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation(credentialError));
        }

        if (!TryCreateUserCredential(ldapServerProfile, username, password, out var credentialToAuthenticate, out var userCredentialError))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation(userCredentialError));
        }

        try
        {
            var requestLabel = $"ldap-gateway-auth:{ldapServerProfile.ProfileId}:{username}:{DateTime.UtcNow:O}";
            var authenticator = new Authenticator(connectionInfo, new NovellLdapConnectionFactoryAdapter());

            var authenticationResult = await authenticator.AuthenticateAsync(
               credentialToAuthenticate,
               searchLimits,
               credentialForSearching,
               requestLabel);

            if (!authenticationResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   authenticationResult.ErrorObject,
                   "Bitai.LDAPHelper Authenticate failed for profile {ProfileId} and user {DomainAccount}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   credentialToAuthenticate.DomainAccountName,
                   authenticationResult.OperationMessage);

                return Result<AuthenticationResultDto>.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(authenticationResult.OperationMessage)
                      ? "LDAP authentication operation failed."
                      : authenticationResult.OperationMessage));
            }

            var authenticatedUsername = authenticationResult.Credential?.DomainAccountName ?? credentialToAuthenticate.DomainAccountName;
            var message = string.IsNullOrWhiteSpace(authenticationResult.OperationMessage)
               ? "LDAP authentication operation completed."
               : authenticationResult.OperationMessage;

            return Result<AuthenticationResultDto>.Success(
               new AuthenticationResultDto(authenticationResult.IsAuthenticated, authenticatedUsername, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while authenticating profile {ProfileId} and username {Username}.",
               ldapServerProfile.ProfileId,
               username);

            return Result<AuthenticationResultDto>.Failure(Error.BadGateway($"LDAP authentication failed: {ex.Message}"));
        }
    }

    public async Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("Username is required."));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("Password is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateUserCredential(ldapServerProfile, username, password, out var credentialToAuthenticate, out var userCredentialError))
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation(userCredentialError));
        }

        try
        {
            var requestLabel = $"ldap-gateway-auth-no-lookup:{ldapServerProfile.ProfileId}:{username}:${DateTime.UtcNow:O}";
            var authenticator = new Authenticator(connectionInfo, new NovellLdapConnectionFactoryAdapter());

            var authenticationResult = await authenticator.AuthenticateAsync(
               credentialToAuthenticate,
               requestLabel);

            if (!authenticationResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   authenticationResult.ErrorObject,
                   "Bitai.LDAPHelper AuthenticateWithoutUserLookup failed for profile {ProfileId} and user {DomainAccount}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   credentialToAuthenticate.DomainAccountName,
                   authenticationResult.OperationMessage);

                return Result<AuthenticationResultDto>.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(authenticationResult.OperationMessage)
                      ? "LDAP authentication operation failed."
                      : authenticationResult.OperationMessage));
            }

            var authenticatedUsername = authenticationResult.Credential?.DomainAccountName ?? credentialToAuthenticate.DomainAccountName;
            var message = string.IsNullOrWhiteSpace(authenticationResult.OperationMessage)
               ? "LDAP authentication operation completed."
               : authenticationResult.OperationMessage;

            return Result<AuthenticationResultDto>.Success(
               new AuthenticationResultDto(authenticationResult.IsAuthenticated, authenticatedUsername, message));
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while authenticating (without lookup) profile {ProfileId} and username {Username}.",
               ldapServerProfile.ProfileId,
               username);

            return Result<AuthenticationResultDto>.Failure(Error.BadGateway($"LDAP authentication failed: {ex.Message}"));
        }
    }
    #endregion


    #region User Provisioning
    public async Task<Result<DirectoryEntryDto>> CreateMsAdUserAsync(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, CreateMsAdUserDto user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (user is null)
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation("User payload is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation(credentialError));
        }

        try
        {
            var requestLabel = $"ldap-gateway-create-msad-user:{ldapServerProfile.ProfileId}:{user.SAMAccountName}:{DateTime.UtcNow:O}";
            var accountManager = new AccountManager(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               new NovellLdapConnectionFactoryAdapter());

            var createResult = await accountManager
               .CreateUserAccountForMsAD(user, requestLabel)
               .WaitAsync(cancellationToken);

            if (!createResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   createResult.ErrorObject,
                   "Bitai.LDAPHelper CreateMsAdUser failed for profile {ProfileId} and account {SamAccountName}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   user.SAMAccountName,
                   createResult.OperationMessage);

                return Result<DirectoryEntryDto>.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(createResult.OperationMessage)
                      ? "LDAP user-creation operation failed."
                      : createResult.OperationMessage));
            }

            var createdUser = createResult.UserAccount ?? user;
            var createdEntry = new DirectoryEntryDto
            {
                distinguishedName = createdUser.DistinguishedName,
                givenName = createdUser.GivenName,
                sn = createdUser.Sn,
                cn = createdUser.Cn,
                name = createdUser.Name,
                displayName = createdUser.DisplayName,
                description = createdUser.Description,
                objectClass = createdUser.ObjectClass,
                samAccountName = createdUser.SAMAccountName,
                userPrincipalName = createdUser.UserPrincipalName,
                userAccountControl = createdUser.UserAccountControl,
                department = createdUser.Department,
                telephoneNumber = createdUser.TelephoneNumber,
                mail = createdUser.Mail
            };

            return Result<DirectoryEntryDto>.Success(createdEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while creating MS AD user for profile {ProfileId} and account {SamAccountName}.",
               ldapServerProfile.ProfileId,
               user.SAMAccountName);

            return Result<DirectoryEntryDto>.Failure(Error.BadGateway($"LDAP user-creation failed: {ex.Message}"));
        }
    }

    public async Task<Result> SetMsAdUserPasswordAsync(
       LdapServerProfileOption ldapServerProfile,
       CatalogType catalogType,
       IdentifierAttribute identifierAttribute,
       string identifier,
       string password,
       bool mustChangeAtNextLogon,
       CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result.Failure(Error.Validation("Identifier is required."));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Result.Failure(Error.Validation("Password is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        try
        {
            var requestLabel = $"ldap-gateway-set-msad-password:{ldapServerProfile.ProfileId}:{identifier}:{DateTime.UtcNow:O}";
            var accountManager = new AccountManager(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               new NovellLdapConnectionFactoryAdapter());

            var setPasswordResult = await accountManager
               .SetMsADUserAccountPassword(resolvedIdentifierAttribute, identifier, password, requestLabel, mustChangeAtNextLogon)
               .WaitAsync(cancellationToken);

            if (!setPasswordResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   setPasswordResult.ErrorObject,
                   "Bitai.LDAPHelper SetMsAdUserPassword failed for profile {ProfileId}, identifier {Identifier}, attribute {IdentifierAttribute}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   identifier,
                   resolvedIdentifierAttribute,
                   setPasswordResult.OperationMessage);

                return Result.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(setPasswordResult.OperationMessage)
                      ? "LDAP password-update operation failed."
                      : setPasswordResult.OperationMessage));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while setting MS AD password for profile {ProfileId} and identifier {Identifier}.",
               ldapServerProfile.ProfileId,
               identifier);

            return Result.Failure(Error.BadGateway($"LDAP password-update failed: {ex.Message}"));
        }
    }

    public async Task<Result> DisableMsAdUserAsync(
       LdapServerProfileOption ldapServerProfile,
       CatalogType catalogType,
       IdentifierAttribute identifierAttribute,
       string identifier,
       string? reason,
       CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        try
        {
            var requestLabel = $"ldap-gateway-disable-msad-user:{ldapServerProfile.ProfileId}:{identifier}:{DateTime.UtcNow:O}";
            var accountManager = new AccountManager(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               new NovellLdapConnectionFactoryAdapter());

            var disableResult = await accountManager
               .DisableMsADUserAccount(resolvedIdentifierAttribute, identifier, requestLabel)
               .WaitAsync(cancellationToken);

            if (!disableResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   disableResult.ErrorObject,
                   "Bitai.LDAPHelper DisableMsAdUser failed for profile {ProfileId}, identifier {Identifier}, attribute {IdentifierAttribute}, reason {Reason}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   identifier,
                   resolvedIdentifierAttribute,
                   reason,
                   disableResult.OperationMessage);

                return Result.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(disableResult.OperationMessage)
                      ? "LDAP disable-user operation failed."
                      : disableResult.OperationMessage));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while disabling MS AD user for profile {ProfileId} and identifier {Identifier}. Reason: {Reason}",
               ldapServerProfile.ProfileId,
               identifier,
               reason);

            return Result.Failure(Error.BadGateway($"LDAP disable-user operation failed: {ex.Message}"));
        }
    }

    public async Task<Result> DeleteMsAdUserAsync(
       LdapServerProfileOption ldapServerProfile,
       CatalogType catalogType,
       IdentifierAttribute identifierAttribute,
       string identifier,
       CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        try
        {
            var requestLabel = $"ldap-gateway-delete-msad-user:{ldapServerProfile.ProfileId}:{identifier}:{DateTime.UtcNow:O}";
            var accountManager = new AccountManager(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               new NovellLdapConnectionFactoryAdapter());

            var deleteResult = await accountManager
               .RemoveMsADUserAccount(resolvedIdentifierAttribute, identifier, requestLabel)
               .WaitAsync(cancellationToken);

            if (!deleteResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   deleteResult.ErrorObject,
                   "Bitai.LDAPHelper DeleteMsAdUser failed for profile {ProfileId}, identifier {Identifier}, attribute {IdentifierAttribute}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   identifier,
                   resolvedIdentifierAttribute,
                   deleteResult.OperationMessage);

                return Result.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(deleteResult.OperationMessage)
                      ? "LDAP delete-user operation failed."
                      : deleteResult.OperationMessage));
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while deleting MS AD user for profile {ProfileId} and identifier {Identifier}.",
               ldapServerProfile.ProfileId,
               identifier);

            return Result.Failure(Error.BadGateway($"LDAP delete-user operation failed: {ex.Message}"));
        }
    }
    #endregion


    #region Generic Directory Search Methods
    public async Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        IdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<DirectoryEntryDto>.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = $"ldap-gateway-get-entry:{ldapServerProfile.ProfileId}:{identifier}:{DateTime.UtcNow:O}";
            var searcher = new Searcher(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               new NovellLdapConnectionFactoryAdapter());

            var filter = new AttributeFilter(resolvedIdentifierAttribute, new FilterValue(identifier));
            var searchResult = await searcher
                    .SearchEntriesAsync(filter, resolvedRequiredAttributes, requestLabel)
               .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   searchResult.ErrorObject,
                   "Bitai.LDAPHelper GetDirectoryEntry failed for profile {ProfileId}, identifier {Identifier}, attribute {IdentifierAttribute}. Message: {OperationMessage}",
                   ldapServerProfile.ProfileId,
                   identifier,
                   resolvedIdentifierAttribute,
                   searchResult.OperationMessage);

                return Result<DirectoryEntryDto>.Failure(
                   Error.BadGateway(string.IsNullOrWhiteSpace(searchResult.OperationMessage)
                      ? "LDAP get-entry operation failed."
                      : searchResult.OperationMessage));
            }

            var entries = (searchResult.Entries ?? Array.Empty<LDAPEntry>()).ToList();
            if (entries.Count == 0)
            {
                return Result<DirectoryEntryDto>.Failure(
                   Error.NotFound($"Directory entry not found for {identifierAttribute}='{identifier}'."));
            }

            if (entries.Count > 1)
            {
                return Result<DirectoryEntryDto>.Failure(
                   Error.Validation($"More than one LDAP entry was found for {identifierAttribute}='{identifier}'."));
            }

            var entry = entries[0];
            return Result<DirectoryEntryDto>.Success(MapToDirectoryEntryDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unhandled exception while getting directory entry for profile {ProfileId}, identifier {Identifier}, attribute {IdentifierAttribute}.",
               ldapServerProfile.ProfileId,
               identifier,
               identifierAttribute);

            return Result<DirectoryEntryDto>.Failure(Error.BadGateway($"LDAP get-entry operation failed: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        int sizeLimit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.Validation("LDAP server profile is required."));
        }

        if (string.IsNullOrWhiteSpace(filterValue))
        {
            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.Validation("Filter is required."));
        }

        if (sizeLimit <= 0)
        {
            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.Validation("SizeLimit must be greater than zero."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.Validation(credentialError));
        }

        searchLimits.MaxSearchResults = sizeLimit;

        try
        {
            var requestLabel = $"ldap-gateway-search-directory:{ldapServerProfile.ProfileId}:{DateTime.UtcNow:O}";

            var combinedFilter = CreateCombinedFilterObject(false, filterAttribute, filterValue, true, secondaryFilterAttribute, secondaryFilterValue);

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                new NovellLdapConnectionFactoryAdapter());

            var searchResult = await searcher
                .SearchEntriesAsync(combinedFilter, RequiredEntryAttributes.Few, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "Bitai.LDAPHelper SearchDirectory failed for profile {ProfileId}, filter {Filter}, sizeLimit {SizeLimit}. Message: {OperationMessage}",
                    ldapServerProfile.ProfileId,
                    combinedFilter,
                    sizeLimit,
                    searchResult.OperationMessage);

                return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(
                    Error.BadGateway(string.IsNullOrWhiteSpace(searchResult.OperationMessage)
                        ? "LDAP search-directory operation failed."
                        : searchResult.OperationMessage));
            }

            var mappedEntries = (searchResult.Entries ?? Array.Empty<LDAPEntry>())
                .Select(MapToDirectoryEntryDto)
                .ToList();

            return Result<IReadOnlyList<DirectoryEntryDto>>.Success(mappedEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception while searching directory for profile {ProfileId} with filter {Filter} and sizeLimit {SizeLimit}.",
                ldapServerProfile.ProfileId,
                filterValue,
                sizeLimit);

            return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(Error.BadGateway($"LDAP search-directory operation failed: {ex.Message}"));
        }
    }
    #endregion


    #region User Search Methods
    public Task<Result<IReadOnlyList<DirectoryEntryDto>>> GetUserParentsAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<DirectoryEntryDto>>("GetUserParents");

    public Task<Result<IReadOnlyList<LdapUserDto>>> SearchUsersAsync(string server, CatalogType catalogType, string filter, int sizeLimit, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<LdapUserDto>>("SearchUsers");
    #endregion


    #region Group Search Methods
    public Task<Result<LdapGroupDto>> GetGroupAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
       => NotConfigured<LdapGroupDto>("GetGroup");

    public Task<Result<IReadOnlyList<LdapGroupDto>>> GetGroupParentsAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<LdapGroupDto>>("GetGroupParents");

    public Task<Result<IReadOnlyList<LdapGroupDto>>> SearchGroupsAsync(string server, CatalogType catalogType, string filter, int sizeLimit, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<LdapGroupDto>>("SearchGroups");
    #endregion


    #region Private Helper Methods
    private static bool TryCreateConnectionInfo(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, out ConnectionInfo connectionInfo, out string error)
    {
        connectionInfo = default!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(ldapServerProfile.Server))
        {
            error = $"LDAP server is missing for profile '{ldapServerProfile.ProfileId}'.";
            return false;
        }

        if (!TryGetCatalogNetworkSettings(ldapServerProfile, catalogType, out var selectedUseSslValue, out var selectedPortValue, out var settingsError))
        {
            error = settingsError;
            return false;
        }

        if (!TryResolvePort(catalogType, selectedPortValue, selectedUseSslValue, out var resolvedPortNumber))
        {
            error = $"LDAP port '{selectedPortValue}' is invalid for profile '{ldapServerProfile.ProfileId}' and catalog type '{catalogType}'.";
            return false;
        }

        if (ldapServerProfile.ConnectionTimeout <= 0 || ldapServerProfile.ConnectionTimeout > short.MaxValue)
        {
            error = $"ConnectionTimeout must be between 1 and {short.MaxValue} seconds for profile '{ldapServerProfile.ProfileId}'.";
            return false;
        }

        connectionInfo = new ConnectionInfo(
            ldapServerProfile.Server,
            resolvedPortNumber,
            selectedUseSslValue,
            (short)ldapServerProfile.ConnectionTimeout);

        return true;
    }

    private static bool TryCreateSearchLimits(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, out SearchLimits searchLimits, out string error)
    {
        searchLimits = default!;
        error = string.Empty;

        var selectedBaseDn = catalogType == CatalogType.GC
           ? ldapServerProfile.BaseDNforGlobalCatalog
           : ldapServerProfile.BaseDN;

        if (!Enum.IsDefined(typeof(CatalogType), catalogType))
        {
            error = $"Catalog type '{catalogType}' is not supported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedBaseDn))
        {
            var baseDnFieldName = catalogType == CatalogType.GC ? nameof(LdapServerProfileOption.BaseDNforGlobalCatalog) : nameof(LdapServerProfileOption.BaseDN);
            error = $"{baseDnFieldName} is required for profile '{ldapServerProfile.ProfileId}'.";
            return false;
        }

        searchLimits = new SearchLimits(selectedBaseDn);
        return true;
    }

    private static bool TryGetCatalogNetworkSettings(
       LdapServerProfileOption ldapServerProfile,
       CatalogType catalogType,
       out bool selectedUseSslValue,
       out string selectedPortValue,
       out string error)
    {
        selectedUseSslValue = false;
        selectedPortValue = string.Empty;
        error = string.Empty;

        if (!Enum.IsDefined(typeof(CatalogType), catalogType))
        {
            error = $"Catalog type '{catalogType}' is not supported.";
            return false;
        }

        if (catalogType == CatalogType.GC)
        {
            selectedUseSslValue = ldapServerProfile.UseSSLforGlobalCatalog;
            selectedPortValue = ldapServerProfile.PortForGlobalCatalog;
            return true;
        }

        selectedUseSslValue = ldapServerProfile.UseSSL;
        selectedPortValue = ldapServerProfile.Port;
        return true;
    }

    private static bool TryCreateConnectionCredential(LdapServerProfileOption ldapServerProfile, out LDAPDomainAccountCredential credential, out string error)
    {
        credential = default!;
        error = string.Empty;

        if (!TryResolveDomainAndAccount(ldapServerProfile.BindAccountName, ldapServerProfile.DefaultDomainName, out var resolvedDomainName, out var resolvedAccountName, out error))
        {
            error = $"Invalid BindAccountName for profile '{ldapServerProfile.ProfileId}'. {error}";
            return false;
        }

        credential = new LDAPDomainAccountCredential(resolvedDomainName, resolvedAccountName, ldapServerProfile.BindAccountPassword ?? string.Empty);
        return true;
    }

    private static bool TryCreateUserCredential(
       LdapServerProfileOption ldapServerProfile,
       string username,
       string password,
       out LDAPDomainAccountCredential credential,
       out string error)
    {
        credential = default!;
        error = string.Empty;

        if (!TryResolveDomainAndAccount(username, ldapServerProfile.DefaultDomainName, out var resolvedDomainName, out var resolvedAccountName, out error))
        {
            return false;
        }

        credential = new LDAPDomainAccountCredential(resolvedDomainName, resolvedAccountName, password);
        return true;
    }

    private static bool TryResolveDomainAndAccount(string domainAccountName, string defaultDomainName, out string resolvedDomainName, out string resolvedAccountName, out string error)
    {
        resolvedDomainName = string.Empty;
        resolvedAccountName = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(domainAccountName))
        {
            error = "Domain account name is required.";
            return false;
        }

        var value = domainAccountName.Trim();
        var accountSeparatorIndex = value.IndexOf('\\');

        if (accountSeparatorIndex > -1)
        {
            resolvedDomainName = value[..accountSeparatorIndex].Trim();
            resolvedAccountName = value[(accountSeparatorIndex + 1)..].Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(defaultDomainName))
            {
                error = "DefaultDomainName is required when the account does not include a domain prefix.";
                return false;
            }

            resolvedDomainName = defaultDomainName.Trim();
            resolvedAccountName = value;
        }

        if (string.IsNullOrWhiteSpace(resolvedDomainName) || string.IsNullOrWhiteSpace(resolvedAccountName))
        {
            error = "Both domain and account segments must be present.";
            return false;
        }

        return true;
    }

    private static bool TryResolvePort(CatalogType catalogType, string portValue, bool useSsl, out int port)
    {
        port = 0;

        if (!Enum.IsDefined(typeof(CatalogType), catalogType))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(portValue) || string.Equals(portValue.Trim(), "Default", StringComparison.OrdinalIgnoreCase))
        {
            port = catalogType == CatalogType.GC
               ? useSsl ? 3269 : 3268
               : useSsl ? 636 : 389;
            return true;
        }

        return int.TryParse(portValue, out port) && port > 0 && port <= 65535;
    }

    private static EntryAttribute ResolveIdentifierAttribute(IdentifierAttribute identifierAttribute)
    {
        return identifierAttribute switch
        {
            IdentifierAttribute.DistinguishedName => EntryAttribute.distinguishedName,
            IdentifierAttribute.SAMAccountName => EntryAttribute.sAMAccountName,
            _ => throw new ArgumentOutOfRangeException(nameof(identifierAttribute), $"Unsupported identifier attribute: {identifierAttribute}")
        };
    }

    private static RequiredEntryAttributes ResolveRequiredEntryAttributes(LdapEntryAttributeSet requiredAttributeSet)
    {
        return requiredAttributeSet switch
        {
            LdapEntryAttributeSet.Minimum => RequiredEntryAttributes.Minimun,
            LdapEntryAttributeSet.MinimumWithMember => RequiredEntryAttributes.MinimunWithMember,
            LdapEntryAttributeSet.MinimumWithMemberOf => RequiredEntryAttributes.MinimunWithMemberOf,
            LdapEntryAttributeSet.MinimumWithMemberAndMemberOf => RequiredEntryAttributes.MinimunWithMemberAndMemberOf,
            LdapEntryAttributeSet.Few => RequiredEntryAttributes.Few,
            LdapEntryAttributeSet.FewWithMember => RequiredEntryAttributes.FewWithMember,
            LdapEntryAttributeSet.FewWithMemberOf => RequiredEntryAttributes.FewWithMemberOf,
            LdapEntryAttributeSet.FewWithMemberAndMemberOf => RequiredEntryAttributes.FewWithMemberAndMemberOf,
            LdapEntryAttributeSet.All => RequiredEntryAttributes.All,
            LdapEntryAttributeSet.AllWithMember => RequiredEntryAttributes.AllWithMember,
            LdapEntryAttributeSet.AllWithMemberOf => RequiredEntryAttributes.AllWithMemberOf,
            LdapEntryAttributeSet.AllWithMemberAndMemberOf => RequiredEntryAttributes.AllWithMemberAndMemberOf,
            LdapEntryAttributeSet.MemberAndMemberOf => RequiredEntryAttributes.MemberAndMemberOf,
            LdapEntryAttributeSet.ObjectSidAndSAMAccountName => RequiredEntryAttributes.ObjectSidAndSAMAccountName,
            LdapEntryAttributeSet.OnlyMember => RequiredEntryAttributes.OnlyMember,
            LdapEntryAttributeSet.OnlyMemberOf => RequiredEntryAttributes.OnlyMemberOf,
            LdapEntryAttributeSet.OnlyCN => RequiredEntryAttributes.OnlyCN,
            LdapEntryAttributeSet.OnlyObjectSid => RequiredEntryAttributes.OnlyObjectSid,
            _ => throw new ArgumentOutOfRangeException(nameof(requiredAttributeSet), $"Unsupported required attribute set: {requiredAttributeSet}")
        };
    }

    private static DirectoryEntryDto MapToDirectoryEntryDto(LDAPEntry entry)
    {
        return new DirectoryEntryDto
        {
            RequestLabel = entry.RequestLabel,
            c = entry.c,
            cn = entry.cn,
            company = entry.company,
            co = entry.co,
            description = entry.description,
            department = entry.department,
            displayName = entry.displayName,
            distinguishedName = entry.distinguishedName,
            givenName = entry.givenName,
            l = entry.l,
            lastLogon = entry.lastLogon,
            mail = entry.mail,
            manager = entry.manager,
            member = entry.member,
            memberOf = entry.memberOf,
            memberOfEntries = entry.memberOfEntries?.Select(MapToDirectoryEntryDto).ToList(),
            name = entry.name,
            objectCategory = entry.objectCategory,
            objectClass = entry.objectClass,
            samAccountName = entry.samAccountName,
            samAccountType = entry.samAccountType,
            sn = entry.sn,
            telephoneNumber = entry.telephoneNumber,
            title = entry.title,
            userPrincipalName = entry.userPrincipalName,
            whenCreated = entry.whenCreated,
            objectGuid = entry.objectGuid,
            objectGuidBytes = entry.objectGuidBytes,
            objectSid = entry.objectSid,
            objectSidBytes = entry.objectSidBytes,
            userAccountControl = entry.userAccountControl
        };
    }

    private static ICombinableFilter CreateCombinedFilterObject(bool negateResult, LdapEntryAttribute primaryAttribute, string primaryValue, bool? combineWithAnd, LdapEntryAttribute? secondaryAttribute, string? secondaryValue)
    {
        var firstAttributeFilter = new AttributeFilter((EntryAttribute)primaryAttribute, new FilterValue(primaryValue));

        ICombinableFilter combinableFilter;
        if (secondaryAttribute is null || string.IsNullOrWhiteSpace(secondaryValue))
        {
            combinableFilter = CombineFilters(negateResult, true, firstAttributeFilter, null);
            return combinableFilter;
        }

        var secondAttributeFilter = new AttributeFilter((EntryAttribute)secondaryAttribute, new FilterValue(secondaryValue));

        combinableFilter = CombineFilters(negateResult, combineWithAnd ?? true, firstAttributeFilter, secondAttributeFilter);
        return combinableFilter;
    }

    private static ICombinableFilter CombineFilters(bool negateResult, bool? combineWithAnd, ICombinableFilter primaryFilter, ICombinableFilter? secondaryFilter)
    {
        if (secondaryFilter is null)
        {
            return new AttributeFilterCombiner(negateResult, true, new List<ICombinableFilter> { primaryFilter });
        }
        else
        {
            return new AttributeFilterCombiner(negateResult, combineWithAnd ?? true, new List<ICombinableFilter> { primaryFilter, secondaryFilter });
        }
    }

    private Task<Result<T>> NotConfigured<T>(string operation)
    {
        _logger.LogWarning("Bitai.LDAPHelper adapter operation {Operation} is not mapped yet.", operation);
        return Task.FromResult(Result<T>.Failure(Error.BadGateway("Bitai.LDAPHelper adapter is not yet mapped for this operation.")));
    }

    private Task<Result> NotConfigured(string operation)
    {
        _logger.LogWarning("Bitai.LDAPHelper adapter operation {Operation} is not mapped yet.", operation);
        return Task.FromResult(Result.Failure(Error.BadGateway("Bitai.LDAPHelper adapter is not yet mapped for this operation.")));
    }
    #endregion
}
