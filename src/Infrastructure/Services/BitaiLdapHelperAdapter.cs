using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class BitaiLdapHelperAdapter : IBitaiLdapHelperAdapter
{
    private readonly ILogger<BitaiLdapHelperAdapter> _logger;

    public BitaiLdapHelperAdapter(ILogger<BitaiLdapHelperAdapter> logger)
    {
        _logger = logger;
    }

    public Task<Result<AuthenticationResultDto>> AuthenticateAsync(string server, CatalogType catalogType, string username, string password, CancellationToken cancellationToken)
       => NotConfigured<AuthenticationResultDto>("Authenticate");

    public Task<Result<AuthenticationResultDto>> AuthenticateWithoutUserLookupAsync(string server, CatalogType catalogType, string username, string password, CancellationToken cancellationToken)
      => NotConfigured<AuthenticationResultDto>("AuthenticateWithoutUserLookup");

    public Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(string server, CatalogType catalogType, string identifier, string identifierAttribute, CancellationToken cancellationToken)
      => NotConfigured<DirectoryEntryDto>("GetDirectoryEntry");

    public Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(string server, CatalogType catalogType, string filter, int sizeLimit, CancellationToken cancellationToken)
       => NotConfigured<IReadOnlyList<DirectoryEntryDto>>("SearchDirectory");

    public Task<Result<DirectoryEntryDto>> CreateMsAdUserAsync(string server, CatalogType catalogType, CreateMsAdUserDto user, CancellationToken cancellationToken)
       => NotConfigured<DirectoryEntryDto>("CreateMsAdUser");

    public Task<Result> SetMsAdUserPasswordAsync(string server, CatalogType catalogType, string identifier, string password, bool mustChangeAtNextLogon, CancellationToken cancellationToken)
       => NotConfigured("SetMsAdUserPassword");

    public Task<Result> DisableMsAdUserAsync(string server, CatalogType catalogType, string identifier, string? reason, CancellationToken cancellationToken)
       => NotConfigured("DisableMsAdUser");

    public Task<Result> DeleteMsAdUserAsync(string server, CatalogType catalogType, string identifier, CancellationToken cancellationToken)
       => NotConfigured("DeleteMsAdUser");

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
