---
work_item: remove-secrets-from-appsettings
intent: code-review-20251023-remediation
created: 2026-10-24T03:00:00Z
mode: validate
checkpoint_1: approved
---

# Design: Remove secrets from appsettings.json

## Summary

`appsettings.json` stays tracked with static profiles (constitution) but every `BindAccountPassword` becomes an empty string. Passwords are supplied at deploy time through environment variables or user-secrets. The service fails at startup when a profile has no bind password and the Novell adapter is in use.

## Scope

**In Scope:**
- Blank `BindAccountPassword` in tracked `src/Api/appsettings.json`.
- Startup validation of bind passwords (`IValidateOptions` + `ValidateOnStart`).
- `UserSecretsId` in the Api csproj.
- Tests for the validator and for startup in Testing with empty passwords.
- Documentation of secret configuration and manual follow-ups.

**Out of Scope:**
- Rotating passwords and cleaning git history (manual operator tasks).
- Contents of the untracked `appsettings.Development.json`.
- External `Bitai.LDAPHelper` (C2) and in-memory password protection (M6).
- Changing the profile configuration format.

## Key Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Password in JSON | Empty string, no placeholder | A placeholder would pass a non-empty check; empty fails clearly |
| Secret source | Env vars (`LDAPServerProfiles__<index>__BindAccountPassword`) and user-secrets in Development | Standard ASP.NET Core mechanism, no new code |
| Where to validate | `IValidateOptions<LdapServerProfilesOptions>` with `ValidateOnStart` reading `AdapterType` | The rule depends on the adapter; a plain `.Validate` lambda cannot see it |
| When to require | Only with `AdapterType = NovellLdapAdapter` | Tests and mock mode keep working without a directory |
| Error message | `ProfileId` only, never values | Constitution: credentials are never logged or returned |
| Index-based env vars | Document that the array index is part of the variable name | .NET array binding limitation; a dictionary keyed by `ProfileId` would break the config contract |
| `UserSecretsId` | Add to Api csproj | Enables `dotnet user-secrets` without touching tracked files |

## Technical Approach

### Architecture

```
appsettings.json (no secrets) ─┐
env vars / user-secrets ───────┼─► IConfiguration ─► LdapServerProfilesOptions
                               │                          │
          ServiceConnectionAdapterOptions ─► LdapServerProfilesValidator
                                                          │ fails if Novell and password empty
                                                          ▼
                                                ValidateOnStart (startup aborts)
```

### Files
- Modify: `src/Api/appsettings.json`, `src/Api/Bitai.LDAPGateway.Api.csproj`, `src/Infrastructure/DependencyInjection.cs`.
- Create: `src/Infrastructure/Options/LdapServerProfilesValidator.cs`.
- Tests: validator unit tests (Novell without password fails, with password passes, Mock without password passes, message contains no values); functional startup test in Testing.
- Docs: secret configuration section.

## Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Existing Novell deployment loses its password when the JSON is blanked | High | Document migration; startup fails with a clear message |
| Passwords remain in git history | High | Manual task: rotate and clean history (e.g. `git filter-repo`) |
| Profile order changes the env var name | Medium | Document with an example |
| `appsettings.Development.json` keeps real passwords | Low | Ignored by git; recommend moving to user-secrets |

## Implementation Checklist

- [ ] Create `LdapServerProfilesValidator` and register it with `ValidateOnStart`
- [ ] Add validator unit tests
- [ ] Add `UserSecretsId` to the Api csproj
- [ ] Blank both passwords in `appsettings.json`
- [ ] Functional startup test in Testing
- [ ] Document secret configuration and manual follow-ups
- [ ] Build (warnings as errors) and run the full test suite

## Manual follow-ups (operator)

Rotate EDU/AIR and Development passwords, clean git history, review existing clones and forks.
