# Bitai.LDAPGateway

Stateless REST gateway that exposes configured LDAP server profiles: authentication, directory search, user and group lookups, and Microsoft Active Directory provisioning (create, set password, disable, delete).

Built with .NET 10, Clean Architecture (Domain, Application, Infrastructure, Api), Wolverine and FluentValidation.

## Build and test

```
dotnet build Bitai.LDAPGateway.slnx
dotnet test Bitai.LDAPGateway.slnx
```

Tests run against the mock LDAP adapter and do not need a live directory. Warnings are treated as errors.

## Configuration

LDAP server profiles are static entries in `src/Api/appsettings.json` (`LDAPServerProfiles`). The adapter is selected with `ServiceConnectionAdapter:AdapterType`:

| Value | Behavior |
|-------|----------|
| `MockAdapter` | In-memory directory, no bind password required (default, used by tests) |
| `NovellLdapAdapter` | Real directory; every profile must have a bind password |

### Secrets

`appsettings.json` is tracked and must never contain credentials: `BindAccountPassword` is an empty string for every profile. With `NovellLdapAdapter`, the service **fails at startup** when a profile has no bind password. The error names the profile only, never a value.

Supply the passwords outside the repository.

**Environment variables** (deployment). The number is the zero-based position of the profile in the `LDAPServerProfiles` array, so reordering profiles changes the variable names:

```
LDAPServerProfiles__0__BindAccountPassword=<password for the first profile>
LDAPServerProfiles__1__BindAccountPassword=<password for the second profile>
```

**User secrets** (local development):

```
dotnet user-secrets set "LDAPServerProfiles:0:BindAccountPassword" "<password>" --project src/Api
```

When API security bypass is disabled (`ApiScopesConfiguration:BypassApiScopesAuthorization` is `false`), the Swagger UI client secret is also required:

```
dotnet user-secrets set "SwaggerUIApiClient:SwaggerUIClientSecret" "<secret>" --project src/Api
```

Untracked files such as `appsettings.Development.json` are ignored by git, but prefer user secrets over writing passwords in them.

### Migrating an existing deployment

Deployments that relied on the passwords committed in `appsettings.json` must now set the environment variables above before upgrading, otherwise the service will not start.

## Security follow-ups (manual)

The previous passwords remain in the git history and must be treated as compromised:

1. Rotate the bind account passwords of every profile that was ever committed, and any others used in `appsettings.Development.json`.
2. Remove the old values from the git history (for example with `git filter-repo`) and force-push, then ask contributors to re-clone.
3. Review existing clones, forks and CI caches.
4. Do not commit documents that quote credentials (for example code review notes).
