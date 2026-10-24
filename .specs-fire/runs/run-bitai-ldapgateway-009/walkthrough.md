# Walkthrough: run-bitai-ldapgateway-009

## What changed
- `src/Infrastructure/Options/LdapServerProfilesValidator.cs` (new): with `NovellLdapAdapter`, fails startup for every profile with a blank bind password; names the profile only.
- `src/Infrastructure/DependencyInjection.cs`: registers the validator (picked up by the existing `ValidateOnStart`).
- `src/Api/appsettings.json`: both `BindAccountPassword` values are now `""`.
- `src/Api/Bitai.LDAPGateway.Api.csproj`: `UserSecretsId`.
- `README.md` (new): build/test, configuration, secrets via env vars / user-secrets, migration and manual follow-ups.
- Tests: `LdapServerProfilesValidatorTests` (6).

## Operator actions
Set `LDAPServerProfiles__<index>__BindAccountPassword` before deploying with the Novell adapter. Rotate the exposed passwords and clean git history.
