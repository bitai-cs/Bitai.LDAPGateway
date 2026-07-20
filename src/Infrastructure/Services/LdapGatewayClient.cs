using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class LdapGatewayClient : ILdapGatewayClient
{
   private readonly IOptionsMonitor<LdapServerProfilesOptions> _options;
   private readonly IBitaiLdapHelperAdapter _adapter;

   public LdapGatewayClient(IOptionsMonitor<LdapServerProfilesOptions> options, IBitaiLdapHelperAdapter adapter)
   {
      _options = options;
      _adapter = adapter;
   }

   public async Task<Result<AuthenticationResultDto>> AuthenticateAsync(LdapRequestContext context, string username, string password, bool validateUserExists, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<AuthenticationResultDto>.Failure(profileResult.Error!);
      }

      return await _adapter.AuthenticateAsync(profileResult.Value!, context.CatalogType, username, password, validateUserExists, cancellationToken);
   }

   public async Task<Result<DirectoryEntryDto>> GetDirectoryEntryAsync(LdapRequestContext context, string identifier, string identifierAttribute, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<DirectoryEntryDto>.Failure(profileResult.Error!);
      }

      return await _adapter.GetDirectoryEntryAsync(profileResult.Value!, context.CatalogType, identifier, identifierAttribute, cancellationToken);
   }

   public async Task<Result<IReadOnlyList<DirectoryEntryDto>>> SearchDirectoryAsync(LdapRequestContext context, string filter, int sizeLimit, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(profileResult.Error!);
      }

      return await _adapter.SearchDirectoryAsync(profileResult.Value!, context.CatalogType, filter, sizeLimit, cancellationToken);
   }

   public async Task<Result<DirectoryEntryDto>> CreateMsAdUserAsync(LdapRequestContext context, CreateMsAdUserDto request, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<DirectoryEntryDto>.Failure(profileResult.Error!);
      }

      return await _adapter.CreateMsAdUserAsync(profileResult.Value!, context.CatalogType, request, cancellationToken);
   }

   public async Task<Result> SetMsAdUserPasswordAsync(LdapRequestContext context, string identifier, string newPassword, bool mustChangeAtNextLogon, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result.Failure(profileResult.Error!);
      }

      return await _adapter.SetMsAdUserPasswordAsync(profileResult.Value!, context.CatalogType, identifier, newPassword, mustChangeAtNextLogon, cancellationToken);
   }

   public async Task<Result> DisableMsAdUserAsync(LdapRequestContext context, string identifier, string? reason, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result.Failure(profileResult.Error!);
      }

      return await _adapter.DisableMsAdUserAsync(profileResult.Value!, context.CatalogType, identifier, reason, cancellationToken);
   }

   public async Task<Result> DeleteMsAdUserAsync(LdapRequestContext context, string identifier, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result.Failure(profileResult.Error!);
      }

      return await _adapter.DeleteMsAdUserAsync(profileResult.Value!, context.CatalogType, identifier, cancellationToken);
   }

   public async Task<Result<IReadOnlyList<DirectoryEntryDto>>> GetUserParentsAsync(LdapRequestContext context, string identifier, string identifierAttribute, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<IReadOnlyList<DirectoryEntryDto>>.Failure(profileResult.Error!);
      }

      return await _adapter.GetUserParentsAsync(profileResult.Value!, context.CatalogType, identifier, identifierAttribute, cancellationToken);
   }

   public async Task<Result<IReadOnlyList<LdapUserDto>>> SearchUsersAsync(LdapRequestContext context, string filter, int sizeLimit, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<IReadOnlyList<LdapUserDto>>.Failure(profileResult.Error!);
      }

      return await _adapter.SearchUsersAsync(profileResult.Value!, context.CatalogType, filter, sizeLimit, cancellationToken);
   }

   public async Task<Result<LdapGroupDto>> GetGroupAsync(LdapRequestContext context, string identifier, string identifierAttribute, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<LdapGroupDto>.Failure(profileResult.Error!);
      }

      return await _adapter.GetGroupAsync(profileResult.Value!, context.CatalogType, identifier, identifierAttribute, cancellationToken);
   }

   public async Task<Result<IReadOnlyList<LdapGroupDto>>> GetGroupParentsAsync(LdapRequestContext context, string identifier, string identifierAttribute, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<IReadOnlyList<LdapGroupDto>>.Failure(profileResult.Error!);
      }

      return await _adapter.GetGroupParentsAsync(profileResult.Value!, context.CatalogType, identifier, identifierAttribute, cancellationToken);
   }

   public async Task<Result<IReadOnlyList<LdapGroupDto>>> SearchGroupsAsync(LdapRequestContext context, string filter, int sizeLimit, CancellationToken cancellationToken)
   {
      var profileResult = GetProfile(context.ServerProfile);
      if (!profileResult.IsSuccess)
      {
         return Result<IReadOnlyList<LdapGroupDto>>.Failure(profileResult.Error!);
      }

      return await _adapter.SearchGroupsAsync(profileResult.Value!, context.CatalogType, filter, sizeLimit, cancellationToken);
   }

   private Result<string> GetProfile(string profileId)
   {
      var profile = _options.CurrentValue
         .FirstOrDefault(x => string.Equals(x.ProfileId, profileId, StringComparison.OrdinalIgnoreCase));

      return profile is null
         ? Result<string>.Failure(Error.NotFound($"LDAP server profile '{profileId}' was not found."))
         : Result<string>.Success(profile.Server);
   }
}
