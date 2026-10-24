using Microsoft.Extensions.Options;

namespace Bitai.LDAPGateway.Infrastructure.Options;

/// <summary>
/// Validates that every LDAP server profile has a bind password when a real directory adapter is in use.
/// Failure messages name the profile only and never include credential values.
/// </summary>
public sealed class LdapServerProfilesValidator : IValidateOptions<LdapServerProfilesOptions>
{
    private readonly IOptions<ServiceConnectionAdapterOptions> _adapterOptions;

    public LdapServerProfilesValidator(IOptions<ServiceConnectionAdapterOptions> adapterOptions)
    {
        _adapterOptions = adapterOptions;
    }

    public ValidateOptionsResult Validate(string? name, LdapServerProfilesOptions options)
    {
        if (_adapterOptions.Value.AdapterType != LdapAdapterType.NovellLdapAdapter)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = options
            .Where(profile => string.IsNullOrWhiteSpace(profile.BindAccountPassword))
            .Select(profile => $"LDAP server profile '{profile.ProfileId}' has no BindAccountPassword. "
                + "Supply it through an environment variable or user-secrets (see README.md).")
            .ToList();

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
