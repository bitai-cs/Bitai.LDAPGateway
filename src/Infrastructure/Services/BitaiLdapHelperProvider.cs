using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Infrastructure.Options;
using Bitai.LDAPHelper;
using Bitai.LDAPHelper.DTO;
using Bitai.LDAPHelper.LdapAdapters;
using Bitai.LDAPHelper.QueryFilters;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class BitaiLdapHelperProvider : IDirectoryServiceProvider
{
    private readonly ILogger<BitaiLdapHelperProvider> _logger;
    private readonly ILdapConnectionFactoryAdapter _ldapConnectionFactoryAdapter;



    public BitaiLdapHelperProvider(
        ILogger<BitaiLdapHelperProvider> logger,
        ILdapConnectionFactoryAdapter ldapConnectionFactoryAdapter)
    {
        _logger = logger;
        _ldapConnectionFactoryAdapter = ldapConnectionFactoryAdapter;
    }



    #region Authentication Methods
    public async Task<Result<AuthenticationResultDto>> AuthenticateAsync(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("Directory Server Profile is required."));
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
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var authenticator = new Authenticator(connectionInfo, _ldapConnectionFactoryAdapter);
            
            var authenticationResult = await authenticator.AuthenticateAsync(
                credentialToAuthenticate,
                searchLimits,
                credentialForSearching,
                requestLabel);

            if (!authenticationResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    authenticationResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Username: {Username}, UserToAuthenticate: {UserToAuthenticate}, SearchLimits: {SearchLimits}, UserForSearching: {UserForSearching}.",
                    nameof(Authenticator), nameof(Authenticator.AuthenticateAsync),
                    authenticationResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    username,
                    credentialToAuthenticate.DomainAccountName,
                    searchLimits,
                    credentialForSearching.DomainAccountName);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.AuthenticateAsync)} failed to authenticate user {credentialToAuthenticate.DomainAccountName} due to an error in {nameof(Authenticator)}.{nameof(Authenticator.AuthenticateAsync)}. {authenticationResult.OperationMessage}");

                if (authenticationResult.HasErrorObject)
                {
                    if (authenticationResult.ErrorObject is LdapException ldapExc)                    
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));                    
                    else
                        error = error.WithInner(Error.InnerErr(authenticationResult.ErrorObject));
                }

                return Result<AuthenticationResultDto>.Failure(error);
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
               "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Username:{Username}.",
               nameof(BitaiLdapHelperProvider),
               nameof(BitaiLdapHelperProvider.AuthenticateAsync),
               ldapServerProfile.ProfileId,
               catalogType,
               username);

            return Result<AuthenticationResultDto>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.AuthenticateAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, string username, string password, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<AuthenticationResultDto>.Failure(Error.Validation("Directory Server Profile is required."));
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
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var authenticator = new Authenticator(connectionInfo, _ldapConnectionFactoryAdapter);

            var authenticationResult = await authenticator.AuthenticateAsync(
               credentialToAuthenticate,
               requestLabel);

            if (!authenticationResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   authenticationResult.ErrorObject,
                   "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Username: {Username}, UserToAuthenticate: {UserToAuthenticate}.",
                   nameof(Authenticator), nameof(Authenticator.AuthenticateAsync),
                   authenticationResult.OperationMessage,
                   ldapServerProfile.ProfileId,
                   catalogType,
                   username,
                   credentialToAuthenticate.DomainAccountName);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.AuthenticateAsync)} failed to authenticate user {credentialToAuthenticate.DomainAccountName} due to an error in {nameof(Authenticator)}.{nameof(Authenticator.AuthenticateAsync)}. {authenticationResult.OperationMessage}");

                if (authenticationResult.HasErrorObject)
                {
                    if (authenticationResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(authenticationResult.ErrorObject));                
                }

                return Result<AuthenticationResultDto>.Failure(error);
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
               "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Username: {Username}.",
               nameof(BitaiLdapHelperProvider), nameof(BitaiLdapHelperProvider.AuthenticateAsync),
               ldapServerProfile.ProfileId,
               catalogType,
               username);

            return Result<AuthenticationResultDto>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.AuthenticateAsync)}.", Error.InnerErr(ex)));
        }
    }
    #endregion



    #region User Provisioning
    public async Task<Result<LdapEntryDto>> CreateMsAdUserAsync(LdapServerProfileOption ldapServerProfile, CatalogType catalogType, CreateMsAdUserDto user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<LdapEntryDto>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (user is null)
        {
            return Result<LdapEntryDto>.Failure(Error.Validation("User payload is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<LdapEntryDto>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<LdapEntryDto>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<LdapEntryDto>.Failure(Error.Validation(credentialError));
        }

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var accountManager = new AccountManager(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               _ldapConnectionFactoryAdapter);

            var createResult = await accountManager
               .CreateUserAccountForMsAD(user, requestLabel)
               .WaitAsync(cancellationToken);

            if (!createResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   createResult.ErrorObject,
                   "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, UserDN: {UserDN}.",
                   nameof(AccountManager), nameof(AccountManager.CreateUserAccountForMsAD),
                   createResult.OperationMessage,
                   ldapServerProfile.ProfileId,
                   catalogType,
                   user.DistinguishedName);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.CreateMsAdUserAsync)} failed to create user {user.SAMAccountName} due to an error in {nameof(AccountManager)}.{nameof(AccountManager.CreateUserAccountForMsAD)}. {createResult.OperationMessage}");

                if (createResult.HasErrorObject)
                {
                    if (createResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(createResult.ErrorObject));
                }

                return Result<LdapEntryDto>.Failure(error);
            }

            var createdUser = createResult.UserAccount ?? user;
            var createdEntry = new LdapEntryDto
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

            return Result<LdapEntryDto>.Success(createdEntry);
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, UserDN: {UserDN}.",
               nameof(BitaiLdapHelperProvider), nameof(BitaiLdapHelperProvider.CreateMsAdUserAsync),
               ldapServerProfile.ProfileId,
               catalogType,
               user.DistinguishedName);            

            return Result<LdapEntryDto>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.CreateMsAdUserAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result> SetMsAdUserPasswordAsync(
       LdapServerProfileOption ldapServerProfile,
       CatalogType catalogType,
       LdapIdentifierAttribute identifierAttribute,
       string identifier,
       string password,
       bool mustChangeAtNextLogon,
       CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result.Failure(Error.Validation("Directory Server Profile is required."));
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
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var accountManager = new AccountManager(
               connectionInfo,
               searchLimits,
               credentialForSearching,
               _ldapConnectionFactoryAdapter);

            var setPasswordResult = await accountManager
               .SetMsADUserAccountPassword(resolvedIdentifierAttribute, identifier, password, requestLabel, mustChangeAtNextLogon)
               .WaitAsync(cancellationToken);

            if (!setPasswordResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                   setPasswordResult.ErrorObject,
                   "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}.",
                   nameof(AccountManager), nameof(AccountManager.SetMsADUserAccountPassword),
                   setPasswordResult.OperationMessage,
                   ldapServerProfile.ProfileId,
                   catalogType,
                   resolvedIdentifierAttribute,
                   identifier);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.SetMsAdUserPasswordAsync)} failed to set password for user {identifier} due to an error in {nameof(AccountManager)}.{nameof(AccountManager.SetMsADUserAccountPassword)}. {setPasswordResult.OperationMessage}");

                if (setPasswordResult.HasErrorObject)
                {
                    if (setPasswordResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(setPasswordResult.ErrorObject));
                }

                return Result.Failure(error);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
               ex,
               "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Identifier: {Identifier}.",
               nameof(BitaiLdapHelperProvider),
               nameof(BitaiLdapHelperProvider.SetMsAdUserPasswordAsync),
               ldapServerProfile.ProfileId,
               catalogType,
               identifier);

            return Result.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.SetMsAdUserPasswordAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result> DisableMsAdUserAsync(
       LdapServerProfileOption ldapServerProfile,
       CatalogType catalogType,
       LdapIdentifierAttribute identifierAttribute,
       string identifier,
       string? reason,
       CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result.Failure(Error.Validation("Directory Server Profile is required."));
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
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var accountManager = new AccountManager(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var disableResult = await accountManager
                .DisableMsADUserAccount(resolvedIdentifierAttribute, identifier, requestLabel)
                .WaitAsync(cancellationToken);

            if (!disableResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    disableResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, Reason: {Reason}.",
                    nameof(AccountManager), nameof(AccountManager.DisableMsADUserAccount),
                    disableResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    reason);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.DisableMsAdUserAsync)} failed to disable user {identifier} due to an error in {nameof(AccountManager)}.{nameof(AccountManager.DisableMsADUserAccount)}. {disableResult.OperationMessage}");

                if (disableResult.HasErrorObject)
                {
                    if (disableResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(disableResult.ErrorObject));
                }

                return Result.Failure(error);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Identifier: {Identifier}, Reason: {Reason}",
                nameof(BitaiLdapHelperProvider),
                nameof(BitaiLdapHelperProvider.DisableMsAdUserAsync),
                ldapServerProfile.ProfileId,
                catalogType,
                identifier,
                reason);

            return Result.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.DisableMsAdUserAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result> DeleteMsAdUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result.Failure(Error.Validation("Directory Server Profile is required."));
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
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var accountManager = new AccountManager(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var deleteResult = await accountManager
                .RemoveMsADUserAccount(resolvedIdentifierAttribute, identifier, requestLabel)
                .WaitAsync(cancellationToken);

            if (!deleteResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    deleteResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}.",
                    nameof(AccountManager), nameof(AccountManager.RemoveMsADUserAccount),
                    deleteResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.DeleteMsAdUserAsync)} failed to delete user {identifier} due to an error in {nameof(AccountManager)}.{nameof(AccountManager.RemoveMsADUserAccount)}. {deleteResult.OperationMessage}");

                if (deleteResult.HasErrorObject)
                {
                    if (deleteResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(deleteResult.ErrorObject));
                }

                return Result.Failure(error);
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Identifier: {Identifier}.",
                nameof(BitaiLdapHelperProvider),
                nameof(BitaiLdapHelperProvider.DeleteMsAdUserAsync),
                ldapServerProfile.ProfileId,
                catalogType,
                identifier);

            return Result.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.DeleteMsAdUserAsync)}.", Error.InnerErr(ex)));
        }
    }
    #endregion



    #region Generic Directory Search Methods
    public async Task<Result<LdapEntryDto?>> GetDirectoryEntryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var filterObject = CreateFilterCombiner(false, resolvedIdentifierAttribute, identifier);

            var searchResult = await searcher
                    .SearchEntriesAsync(filterObject, resolvedRequiredAttributes, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetDirectoryEntryAsync)} failed to retrieve directory entry for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<LdapEntryDto?>.Failure(error);
            }

            var entries = (searchResult.Entries ?? Array.Empty<LDAPEntry>()).ToList();

            if (entries.Count > 1)
            {
                return Result<LdapEntryDto?>.Failure(
                    Error.Validation($"More than one LDAP entry was found for {identifierAttribute}={identifier}."));
            }

            var entry = entries.FirstOrDefault();

            return Result<LdapEntryDto?>.Success(
                entry is null ? null : MapToDirectoryEntryDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Identifier: {Identifier}, IdentifierAttribute: {IdentifierAttribute}, RequiredAttributes: {RequiredAttributes}.",
                nameof(BitaiLdapHelperProvider),
                nameof(BitaiLdapHelperProvider.GetDirectoryEntryAsync),
                ldapServerProfile.ProfileId,
                catalogType,
                identifier,
                identifierAttribute,
                resolvedRequiredAttributes);

            return Result<LdapEntryDto?>.Failure(
                Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetDirectoryEntryAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> SearchDirectoryAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(filterValue))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Filter is required."));
        }        

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(credentialError));
        }

        if (secondaryFilterAttribute.HasValue && (string.IsNullOrWhiteSpace(secondaryFilterValue) || combineFilters == null))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("SecondaryFilterValue and CombineFilters are required when SecondaryFilterAttribute is provided."));
        }

        if (!secondaryFilterAttribute.HasValue && (!string.IsNullOrWhiteSpace(secondaryFilterValue) || combineFilters != null))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("SecondaryFilterAttribute and CombineFilters are required when SecondaryFilterValue is provided."));
        }

        var resolvedFilterAttribute = ResolveLdapEntryAttribute(filterAttribute);

        var resolvedSecondaryFilterAttribute = secondaryFilterAttribute.HasValue
            ? ResolveLdapEntryAttribute(secondaryFilterAttribute.Value)
            : (EntryAttribute?)null;

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var combinedFilter = CreateFilterCombiner(
                false,
                resolvedFilterAttribute,
                filterValue,
                combineFilters,
                resolvedSecondaryFilterAttribute,
                secondaryFilterValue);

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var searchResult = await searcher
                .SearchEntriesAsync(combinedFilter, RequiredEntryAttributes.Few, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, FilterAttribute: {FilterAttribute}, FilterValue: {FilterValue}, SecondaryFilterAttribute: {SecondaryFilterAttribute}, SecondaryFilterValue: {SecondaryFilterValue}, CombineFilters: {CombineFilters}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedFilterAttribute,
                    filterValue,
                    resolvedSecondaryFilterAttribute,
                    secondaryFilterValue,
                    combineFilters);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.SearchDirectoryAsync)} failed to retrieve directory entries for filter {combinedFilter} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            var mappedEntries = (searchResult.Entries ?? Array.Empty<LDAPEntry>())
                .Select(MapToDirectoryEntryDto)
                .ToList();

            return Result<IReadOnlyList<LdapEntryDto>>.Success(mappedEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, Filter: {Filter}, RequiredAttributeSet: {RequiredAttributeSet}.",
                nameof(BitaiLdapHelperProvider),
                nameof(BitaiLdapHelperProvider.SearchDirectoryAsync),
                ldapServerProfile.ProfileId,
                filterValue,
                requiredAttributeSet);

            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.SearchDirectoryAsync)}.", Error.InnerErr(ex)));
        }
    }
    #endregion



    #region User Search Methods
    public async Task<Result<LdapEntryDto?>> GetUserAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var onlyUsersFilter = AttributeFilterCombiner.CreateOnlyUsersFilterCombiner();

            var identifierFilter = CreateFilterCombiner(false, resolvedIdentifierAttribute, identifier);

            var fullFilter = new AttributeFilterCombiner(false, true, new ICombinableFilter[] { onlyUsersFilter, identifierFilter });

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var searchResult = await searcher.SearchEntriesAsync(fullFilter, resolvedRequiredAttributes, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes);                

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetUserAsync)} failed to retrieve directory user for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<LdapEntryDto?>.Failure(error);
            }

            if (searchResult.Entries.Count() > 1)
            {
                // Log a warning if multiple entries are found for the same identifier
                return Result<LdapEntryDto?>.Failure(
                    Error.Validation($"More than one LDAP entry was found for {identifierAttribute}='{identifier}'."));
            }

            var entry = searchResult.Entries.FirstOrDefault();

            return Result<LdapEntryDto?>.Success(
                entry is null ? null :
                MapToDirectoryEntryDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}.",
                nameof(BitaiLdapHelperProvider),
                nameof(BitaiLdapHelperProvider.GetUserAsync),
                ldapServerProfile.ProfileId,
                identifierAttribute,
                identifier,
                resolvedRequiredAttributes);

            return Result<LdapEntryDto?>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetUserAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> GetUserParentsAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        bool userMustExist,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var filterObject = CreateFilterCombiner(false, resolvedIdentifierAttribute, identifier);

            var entrySearchResult = await searcher.SearchEntriesAsync(filterObject, resolvedRequiredAttributes, requestLabel);
            if (!entrySearchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    entrySearchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}, UserMustExist: {UserMustExist}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    entrySearchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes,
                    userMustExist);                

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetUserParentsAsync)} failed to retrieve directory user for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {entrySearchResult.OperationMessage}");

                if (entrySearchResult.HasErrorObject)
                {
                    if (entrySearchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(entrySearchResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            if (entrySearchResult.Entries.Count() == 0)
            {
                if (userMustExist)
                    return Result<IReadOnlyList<LdapEntryDto>>.Failure(
                        Error.NotFound($"No directory entry was found in the catalog for {resolvedIdentifierAttribute}='{identifier}'."));
                else
                    return Result<IReadOnlyList<LdapEntryDto>>.Success(Array.Empty<LdapEntryDto>());               
            }

            if (entrySearchResult.Entries.Count() > 1)
            {
                // Log a warning if multiple entries are found for the same identifier
                return Result<IReadOnlyList<LdapEntryDto>>.Failure(
                    Error.Validation($"More than one directory entry was found in the catalog for {resolvedIdentifierAttribute}='{identifier}'."));
            }

            var searchResult = await searcher
                    .SearchParentEntriesAsync(entrySearchResult.Entries, resolvedRequiredAttributes, requestLabel)
                    .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}, UserMustExist: {UserMustExist}.",
                    nameof(Searcher), nameof(Searcher.SearchParentEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes,
                    userMustExist);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetUserParentsAsync)} failed to retrieve directory user parents for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchParentEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            var entries = (searchResult.Entries ?? Array.Empty<LDAPEntry>()).ToList();

            return Result<IReadOnlyList<LdapEntryDto>>.Success(ResolveLdapEntries(entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Identifier: {Identifier}, IdentifierAttribute: {IdentifierAttribute}, RequiredAttributes: {RequiredAttributes}, UserMustExist: {UserMustExist}.",
                nameof(BitaiLdapHelperProvider), nameof(GetUserParentsAsync),
                ldapServerProfile.ProfileId, catalogType,
                identifier, identifierAttribute,
                requiredAttributeSet,
                userMustExist);

            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(GetUserParentsAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> SearchUsersAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        string methodCodeName = "get-users";
        string methodFriendlyName = "to get LDAP user entries";

        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(filterValue))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Filter is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(credentialError));
        }

        if (secondaryFilterAttribute.HasValue && (string.IsNullOrWhiteSpace(secondaryFilterValue) || combineFilters == null))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("SecondaryFilterValue and CombineFilters are required when SecondaryFilterAttribute is provided."));
        }

        if (!secondaryFilterAttribute.HasValue && (!string.IsNullOrWhiteSpace(secondaryFilterValue) || combineFilters != null))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("SecondaryFilterAttribute and CombineFilters are required when SecondaryFilterValue is provided."));
        }

        var resolvedFilterAttribute = ResolveLdapEntryAttribute(filterAttribute);

        var resolvedSecondaryFilterAttribute = secondaryFilterAttribute.HasValue
            ? ResolveLdapEntryAttribute(secondaryFilterAttribute.Value)
            : (EntryAttribute?)null;

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var onlyUsersFilter = AttributeFilterCombiner.CreateOnlyUsersFilterCombiner();

            var combinedFilter = CreateFilterCombiner(
                false,
                resolvedFilterAttribute,
                filterValue,
                combineFilters,
                resolvedSecondaryFilterAttribute,
                secondaryFilterValue);

            var filterObject = new AttributeFilterCombiner(false, true, new ICombinableFilter[] { onlyUsersFilter, combinedFilter });

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var searchResult = await searcher
                .SearchEntriesAsync(filterObject, resolvedRequiredAttributes, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, FilterAttribute: {FilterAttribute}, FilterValue: {FilterValue}, SecondaryFilterAttribute: {SecondaryFilterAttribute}, SecondaryFilterValue: {SecondaryFilterValue}, CombineFilters: {CombineFilters}, RequiredAttributes: {RequiredAttributes}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedFilterAttribute,
                    filterValue,
                    resolvedSecondaryFilterAttribute,
                    secondaryFilterValue,
                    combineFilters,
                    resolvedRequiredAttributes);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.SearchUsersAsync)} failed to retrieve directory user entries for filter {filterObject} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            var mappedEntries = (searchResult.Entries ?? Array.Empty<LDAPEntry>())
                .Select(MapToDirectoryEntryDto)
                .ToList();

            return Result<IReadOnlyList<LdapEntryDto>>.Success(mappedEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, FilterAttribute: {FilterAttribute}, FilterValue: {FilterValue}, SecondaryFilterAttribute: {SecondaryFilterAttribute}, SecondaryFilterValue: {SecondaryFilterValue}, CombineFilters: {CombineFilters}, RequiredAttributes: {RequiredAttributes}",
                nameof(BitaiLdapHelperProvider), nameof(SearchUsersAsync),
                ldapServerProfile.ProfileId, catalogType,
                filterAttribute, filterValue,
                secondaryFilterAttribute, secondaryFilterValue,
                combineFilters, requiredAttributeSet);

            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(SearchUsersAsync)}.", Error.InnerErr(ex)));
        }
    }
    #endregion



    #region Group Search Methods
    public async Task<Result<LdapEntryDto?>> GetGroupAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapIdentifierAttribute identifierAttribute,
        string identifier,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<LdapEntryDto?>.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var onlyGroupsFilter = AttributeFilterCombiner.CreateOnlyGroupsFilterCombiner();

            var identifierFilter = CreateFilterCombiner(false, resolvedIdentifierAttribute, identifier);

            var fullFilter = new AttributeFilterCombiner(false, true, new ICombinableFilter[] { onlyGroupsFilter, identifierFilter });

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var searchResult = await searcher.SearchEntriesAsync(fullFilter, resolvedRequiredAttributes, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetGroupAsync)} failed to retrieve directory group entry for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<LdapEntryDto?>.Failure(error);
            }

            if (searchResult.Entries.Count() > 1)
            {
                return Result<LdapEntryDto?>.Failure(
                    Error.Validation($"More than one LDAP entry was found for {identifierAttribute}='{identifier}'."));
            }

            var entry = searchResult.Entries.FirstOrDefault();

            return Result<LdapEntryDto?>.Success(
                entry is null ? null :
                MapToDirectoryEntryDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}",
                nameof(BitaiLdapHelperProvider), nameof(GetGroupAsync),
                ldapServerProfile.ProfileId, catalogType,
                identifierAttribute, identifier,
                requiredAttributeSet);            

            return Result<LdapEntryDto?>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(GetGroupAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> GetGroupParentsAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        string identifier,
        LdapIdentifierAttribute identifierAttribute,
        LdapEntryAttributeSet requiredAttributeSet,
        bool groupMustExist,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Identifier is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(credentialError));
        }

        var resolvedIdentifierAttribute = ResolveIdentifierAttribute(identifierAttribute);

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var searchFilterObject = CreateFilterCombiner(false, resolvedIdentifierAttribute, identifier);

            var searchGroupResult = await searcher.SearchEntriesAsync(
                searchFilterObject,
                resolvedRequiredAttributes,
                requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchGroupResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchGroupResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}, GroupMustExist: {GroupMustExist}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchGroupResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes,
                    groupMustExist);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetGroupParentsAsync)} failed to retrieve directory group entry for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchGroupResult.OperationMessage}");

                if (searchGroupResult.HasErrorObject)
                {
                    if (searchGroupResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchGroupResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            if (searchGroupResult.Entries.Count() == 0)
            {
                if (groupMustExist)
                    return Result<IReadOnlyList<LdapEntryDto>>.Failure(
                        Error.NotFound($"No directory group was found in the catalog for {resolvedIdentifierAttribute}='{identifier}'."));
                else
                    return Result<IReadOnlyList<LdapEntryDto>>.Success(Array.Empty<LdapEntryDto>());
            }

            if (searchGroupResult.Entries.Count() > 1)
            {
                // Log a warning if multiple entries are found for the same identifier
                return Result<IReadOnlyList<LdapEntryDto>>.Failure(
                    Error.Validation($"More than one directory group was found in the catalog for {resolvedIdentifierAttribute}='{identifier}'."));
            }

            // At this point, we have exactly one entry
            var searchResult = await searcher
                    .SearchParentEntriesAsync(searchGroupResult.Entries, resolvedRequiredAttributes, requestLabel)
                    .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, IdentifierAttribute: {IdentifierAttribute}, Identifier: {Identifier}, RequiredAttributes: {RequiredAttributes}, GroupMustExist: {GroupMustExist}.",
                    nameof(Searcher), nameof(Searcher.SearchParentEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedIdentifierAttribute,
                    identifier,
                    resolvedRequiredAttributes,
                    groupMustExist);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.GetGroupParentsAsync)} failed to retrieve directory group entry for identifier {identifier} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchParentEntriesAsync)}. {searchGroupResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            var entries = (searchResult.Entries ?? Array.Empty<LDAPEntry>()).ToList();

            return Result<IReadOnlyList<LdapEntryDto>>.Success(ResolveLdapEntries(entries));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Inexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfileId: {DirectoryServerProfileId}, CatalogType: {CatalogType}, Identifier: {Identifier}, IdentifierAttribute: {IdentifierAttribute}, RequiredAttributes: {RequiredAttributes}, GroupMustExist: {GroupMustExist}",
                nameof(BitaiLdapHelperProvider), nameof(GetGroupParentsAsync), ldapServerProfile.ProfileId, catalogType, identifier, resolvedIdentifierAttribute, resolvedRequiredAttributes, groupMustExist);
            
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(GetGroupParentsAsync)}.", Error.InnerErr(ex)));
        }
    }

    public async Task<Result<IReadOnlyList<LdapEntryDto>>> SearchGroupsAsync(
        LdapServerProfileOption ldapServerProfile,
        CatalogType catalogType,
        LdapEntryAttribute filterAttribute,
        string filterValue,
        LdapEntryAttribute? secondaryFilterAttribute,
        string? secondaryFilterValue,
        bool? combineFilters,
        LdapEntryAttributeSet requiredAttributeSet,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ldapServerProfile is null)
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Directory Server Profile is required."));
        }

        if (string.IsNullOrWhiteSpace(filterValue))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("Filter is required."));
        }

        if (!TryCreateConnectionInfo(ldapServerProfile, catalogType, out var connectionInfo, out var connectionError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(connectionError));
        }

        if (!TryCreateSearchLimits(ldapServerProfile, catalogType, out var searchLimits, out var searchLimitsError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(searchLimitsError));
        }

        if (!TryCreateConnectionCredential(ldapServerProfile, out var credentialForSearching, out var credentialError))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation(credentialError));
        }

        if (secondaryFilterAttribute.HasValue && (string.IsNullOrWhiteSpace(secondaryFilterValue) || combineFilters == null))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("SecondaryFilterValue and CombineFilters are required when SecondaryFilterAttribute is provided."));
        }

        if (!secondaryFilterAttribute.HasValue && (!string.IsNullOrWhiteSpace(secondaryFilterValue) || combineFilters != null))
        {
            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Validation("SecondaryFilterAttribute and CombineFilters are required when SecondaryFilterValue is provided."));
        }

        var resolvedFilterAttribute = ResolveLdapEntryAttribute(filterAttribute);

        var resolvedSecondaryFilterAttribute = secondaryFilterAttribute.HasValue
            ? ResolveLdapEntryAttribute(secondaryFilterAttribute.Value)
            : (EntryAttribute?)null;

        var resolvedRequiredAttributes = ResolveRequiredEntryAttributes(requiredAttributeSet);

        try
        {
            var requestLabel = nameof(BitaiLdapHelperProvider);

            var onlyUsersFilter = AttributeFilterCombiner.CreateOnlyGroupsFilterCombiner();

            var combinedFilter = CreateFilterCombiner(
                false,
                resolvedFilterAttribute,
                filterValue,
                combineFilters,
                resolvedSecondaryFilterAttribute,
                secondaryFilterValue);

            var filterObject = new AttributeFilterCombiner(false, true, new ICombinableFilter[] { onlyUsersFilter, combinedFilter });

            var searcher = new Searcher(
                connectionInfo,
                searchLimits,
                credentialForSearching,
                _ldapConnectionFactoryAdapter);

            var searchResult = await searcher
                .SearchEntriesAsync(filterObject, resolvedRequiredAttributes, requestLabel)
                .WaitAsync(cancellationToken);

            if (!searchResult.IsSuccessfulOperation)
            {
                _logger.LogError(
                    searchResult.ErrorObject,
                    "{ComponentName}.{ComponentMethodName} failed. OperationMessage: {OperationMessage}, DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, FilterAttribute: {FilterAttribute}, FilterValue: {FilterValue}, SecondaryFilterAttribute: {SecondaryFilterAttribute}, SecondaryFilterValue: {SecondaryFilterValue}, CombineFilters: {CombineFilters}, RequiredAttributes: {RequiredAttributes}.",
                    nameof(Searcher), nameof(Searcher.SearchEntriesAsync),
                    searchResult.OperationMessage,
                    ldapServerProfile.ProfileId,
                    catalogType,
                    resolvedFilterAttribute,
                    filterValue,
                    resolvedSecondaryFilterAttribute,
                    secondaryFilterValue,
                    combineFilters,
                    resolvedRequiredAttributes);

                var error = Error.BadGateway($"{nameof(BitaiLdapHelperProvider)}.{nameof(BitaiLdapHelperProvider.SearchGroupsAsync)} failed to retrieve directory group entries for filter {filterObject} due to an error in {nameof(Searcher)}.{nameof(Searcher.SearchEntriesAsync)}. {searchResult.OperationMessage}");

                if (searchResult.HasErrorObject)
                {
                    if (searchResult.ErrorObject is LdapException ldapExc)
                        error = error.WithInner(Error.InnerErr(ldapExc.Message, ldapExc.StackTrace));
                    else
                        error = error.WithInner(Error.InnerErr(searchResult.ErrorObject));
                }

                return Result<IReadOnlyList<LdapEntryDto>>.Failure(error);
            }

            var mappedEntries = (searchResult.Entries ?? Array.Empty<LDAPEntry>())
                .Select(MapToDirectoryEntryDto)
                .ToList();

            return Result<IReadOnlyList<LdapEntryDto>>.Success(mappedEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected exception in {ClassName}.{ClassMethodName}. DirectoryServerProfile: {DirectoryServerProfileId}, CatalogType: {CatalogType}, FilterAttribute: {FilterAttribute}, FilterValue: {FilterValue}, SecondaryFilterAttribute: {SecondaryFilterAttribute}, SecondaryFilterValue: {SecondaryFilterValue}, CombineFilters: {CombineFilters}, RequiredAttributes: {RequiredAttributes}",
                nameof(BitaiLdapHelperProvider), nameof(SearchGroupsAsync),
                ldapServerProfile.ProfileId, catalogType,
                filterAttribute, filterValue,
                secondaryFilterAttribute, secondaryFilterValue,
                combineFilters, requiredAttributeSet);

            return Result<IReadOnlyList<LdapEntryDto>>.Failure(Error.Internal($"Unexpected exception in {nameof(BitaiLdapHelperProvider)}.{nameof(SearchGroupsAsync)}.", Error.InnerErr(ex)));
        }
    }
    #endregion


    #region Private Helper Methods
    private Task<Result<T>> NotConfigured<T>(string operation)
    {
        _logger.LogError("Bitai.LDAPHelper adapter operation {Operation} is not mapped yet.", operation);
        return Task.FromResult(Result<T>.Failure(Error.BadGateway("Bitai.LDAPHelper adapter is not yet mapped for this operation.")));
    }

    private Task<Result> NotConfigured(string operation)
    {
        _logger.LogError("Bitai.LDAPHelper adapter operation {Operation} is not mapped yet.", operation);
        return Task.FromResult(Result.Failure(Error.BadGateway("Bitai.LDAPHelper adapter is not yet mapped for this operation.")));
    }
    #endregion


    #region Private Static Helper Methods
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

    private static EntryAttribute ResolveIdentifierAttribute(LdapIdentifierAttribute identifierAttribute)
    {
        return identifierAttribute switch
        {
            LdapIdentifierAttribute.DistinguishedName => EntryAttribute.distinguishedName,
            LdapIdentifierAttribute.SAMAccountName => EntryAttribute.sAMAccountName,
            _ => throw new ArgumentOutOfRangeException(nameof(identifierAttribute), $"Unsupported identifier attribute: {identifierAttribute}")
        };
    }

    private static EntryAttribute ResolveLdapEntryAttribute(LdapEntryAttribute ldapEntryAttribute)
    {
        if (Enum.TryParse<EntryAttribute>(ldapEntryAttribute.ToString(), out var resolvedAttribute))
        {
            return resolvedAttribute;
        }

        throw new ArgumentOutOfRangeException(nameof(ldapEntryAttribute), $"Unsupported LDAP entry attribute: {ldapEntryAttribute}");
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

    private static LdapEntryDto MapToDirectoryEntryDto(LDAPEntry entry)
    {
        return new LdapEntryDto
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

    private static IReadOnlyList<LdapEntryDto> ResolveLdapEntries(IReadOnlyList<LDAPEntry> entries)
    {
        return entries.Select(MapToDirectoryEntryDto).ToList();
    }

    private static ICombinableFilter CreateFilterCombiner(bool notEqual, EntryAttribute primaryAttribute, string primaryValue, bool? combineWithAnd = null, EntryAttribute? secondaryAttribute = null, string? secondaryValue = null)
    {
        var firstAttributeFilter = new AttributeFilter(primaryAttribute, new FilterValue(primaryValue));

        ICombinableFilter combinableFilter;
        if (secondaryAttribute is null || string.IsNullOrWhiteSpace(secondaryValue))
        {
            combinableFilter = CombineFilters(notEqual, true, firstAttributeFilter, null);
            return combinableFilter;
        }

        var secondAttributeFilter = new AttributeFilter(secondaryAttribute.Value, new FilterValue(secondaryValue));

        combinableFilter = CombineFilters(notEqual, combineWithAnd ?? true, firstAttributeFilter, secondAttributeFilter);
        return combinableFilter;
    }

    private static ICombinableFilter CombineFilters(bool notEqual, bool? combineWithAnd, ICombinableFilter primaryFilter, ICombinableFilter? secondaryFilter)
    {
        if (secondaryFilter is null)
        {
            return new AttributeFilterCombiner(notEqual, true, new List<ICombinableFilter> { primaryFilter });
        }
        else
        {
            return new AttributeFilterCombiner(notEqual, combineWithAnd ?? true, new List<ICombinableFilter> { primaryFilter, secondaryFilter });
        }
    }
    #endregion
}
