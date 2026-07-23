using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Infrastructure.Options;
using Bitai.LDAPHelper;
using Bitai.LDAPHelper.DTO;
using Bitai.LDAPHelper.LdapAdapters.Novell;
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
            var identifier = string.IsNullOrWhiteSpace(createdUser.DistinguishedName)
               ? createdUser.SAMAccountName ?? string.Empty
               : createdUser.DistinguishedName;

            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["distinguishedName"] = createdUser.DistinguishedName ?? string.Empty,
                ["distinguishedNameOfContainer"] = createdUser.DistinguishedNameOfContainer ?? string.Empty,
                ["givenName"] = createdUser.GivenName ?? string.Empty,
                ["sn"] = createdUser.Sn ?? string.Empty,
                ["cn"] = createdUser.Cn ?? string.Empty,
                ["name"] = createdUser.Name ?? string.Empty,
                ["displayName"] = createdUser.DisplayName ?? string.Empty,
                ["description"] = createdUser.Description ?? string.Empty,
                ["objectClass"] = createdUser.ObjectClass is null ? string.Empty : string.Join(',', createdUser.ObjectClass),
                ["samAccountName"] = createdUser.SAMAccountName ?? string.Empty,
                ["userPrincipalName"] = createdUser.UserPrincipalName ?? string.Empty,
                ["userAccountControl"] = createdUser.UserAccountControl ?? string.Empty,
                ["department"] = createdUser.Department ?? string.Empty,
                ["telephoneNumber"] = createdUser.TelephoneNumber ?? string.Empty,
                ["mail"] = createdUser.Mail ?? string.Empty
            };

            return Result<DirectoryEntryDto>.Success(new DirectoryEntryDto(identifier, attributes));
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

    public Task<Result> SetMsAdUserPasswordAsync(string server, CatalogType catalogType, string identifier, string password, bool mustChangeAtNextLogon, CancellationToken cancellationToken)
       => NotConfigured("SetMsAdUserPassword");

    public Task<Result> DisableMsAdUserAsync(string server, CatalogType catalogType, string identifier, string? reason, CancellationToken cancellationToken)
       => NotConfigured("DisableMsAdUser");

    public Task<Result> DeleteMsAdUserAsync(string server, CatalogType catalogType, string identifier, CancellationToken cancellationToken)
       => NotConfigured("DeleteMsAdUser");

    public Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
      => NotConfigured<DirectoryEntryDto>("GetDirectoryEntry");

    public Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(string server, CatalogType catalogType, string filter, int sizeLimit, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<DirectoryEntryDto>>("SearchDirectory");

    public Task<Result<IReadOnlyList<DirectoryEntryDto>>> GetUserParentsAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<DirectoryEntryDto>>("GetUserParents");

    public Task<Result<IReadOnlyList<LdapUserDto>>> SearchUsersAsync(string server, CatalogType catalogType, string filter, int sizeLimit, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<LdapUserDto>>("SearchUsers");

    public Task<Result<LdapGroupDto>> GetGroupAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
       => NotConfigured<LdapGroupDto>("GetGroup");

    public Task<Result<IReadOnlyList<LdapGroupDto>>> GetGroupParentsAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<LdapGroupDto>>("GetGroupParents");

    public Task<Result<IReadOnlyList<LdapGroupDto>>> SearchGroupsAsync(string server, CatalogType catalogType, string filter, int sizeLimit, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<LdapGroupDto>>("SearchGroups");

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
            selectedUseSslValue = bool.TryParse(ldapServerProfile.UseSSLforGlobalCatalog, out var useSslGc) && useSslGc;
            selectedPortValue = ldapServerProfile.PortForGlobalCatalog;
            return true;
        }

        selectedUseSslValue = bool.TryParse(ldapServerProfile.UseSSL, out var useSslLc) && useSslLc;
        selectedPortValue = ldapServerProfile.Port;
        return true;
    }

    private static bool TryCreateConnectionCredential(LdapServerProfileOption ldapServerProfile, out LDAPDomainAccountCredential credential, out string error)
    {
        credential = default!;
        error = string.Empty;

        if (!TryResolveDomainAndAccount(ldapServerProfile.DomainAccountName, ldapServerProfile.DefaultDomainName, out var resolvedDomainName, out var resolvedAccountName, out error))
        {
            error = $"Invalid DomainAccountName for profile '{ldapServerProfile.ProfileId}'. {error}";
            return false;
        }

        credential = new LDAPDomainAccountCredential(resolvedDomainName, resolvedAccountName, ldapServerProfile.DomainAccountPassword ?? string.Empty);
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
}
