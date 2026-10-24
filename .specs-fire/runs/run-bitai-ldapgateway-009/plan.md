---
run: run-bitai-ldapgateway-009
work_item: remove-secrets-from-appsettings
intent: code-review-20251023-remediation
mode: validate
checkpoint: plan
---

# Implementation Plan: Remove secrets from appsettings.json

## Approach

1. Create `LdapServerProfilesValidator : IValidateOptions<LdapServerProfilesOptions>`, injecting `IOptions<ServiceConnectionAdapterOptions>`. With `NovellLdapAdapter`, every profile with a blank `BindAccountPassword` yields a failure naming only the `ProfileId`. With `MockAdapter` it validates nothing.
2. Register it in `AddInfrastructure` (singleton `IValidateOptions`); the existing `ValidateOnStart` on the profiles options runs it.
3. Add `UserSecretsId` to `Bitai.LDAPGateway.Api.csproj`.
4. Set both `BindAccountPassword` values in `src/Api/appsettings.json` to `""`.
5. Create `README.md` at the repo root with a secrets-configuration section and manual follow-ups.

## Files to Create

| File | Purpose |
|------|---------|
| `src/Infrastructure/Options/LdapServerProfilesValidator.cs` | Startup validation of bind passwords |
| `tests/Api.FunctionalTests/LdapServerProfilesValidatorTests.cs` | Validator tests (Application.UnitTests does not reference Infrastructure) |
| `README.md` (repo root) | Project overview plus secrets section: env var / user-secrets usage, index naming, migration, manual follow-ups |

## Files to Modify

| File | Changes |
|------|---------|
| `src/Infrastructure/DependencyInjection.cs` | Register the validator |
| `src/Api/Bitai.LDAPGateway.Api.csproj` | Add `UserSecretsId` |
| `src/Api/appsettings.json` | Blank both passwords |

## Tests

| Test | Coverage |
|------|----------|
| Novell + blank password | Fails; message has the `ProfileId` and no password value |
| Novell + password set | Passes |
| Mock + blank password | Passes |
| Several profiles, one blank | Failure names only the blank one |
| Functional (existing 24 suite, Testing env, empty passwords) | App still starts and tests pass |
| Functional: Novell adapter + blank password | Host start fails with `OptionsValidationException` |

## Technical Details

- Never reads or prints existing real passwords; they are only overwritten with `""`. The old values remain in git history (manual cleanup).
- Env var form: `LDAPServerProfiles__0__BindAccountPassword`, where `0` is the profile's array index.
- Regenerated Wolverine code is not expected to change (no handler changes).
- `appsettings.Development.json` (ignored) is not touched.
