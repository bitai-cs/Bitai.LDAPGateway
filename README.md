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

## Caching

Directory reads are cached with FusionCache: an in-process L1, a shared Redis L2 and a Redis Pub/Sub backplane that drops the L1 of the other instances after a mutation.

### What is cached

| Query | L1 | L2 |
|---|---|---|
| `SearchUsers`, `SearchGroups`, `SearchDirectory` | 30 s | 2 min |
| `GetUserByIdentifier`, `GetGroupByIdentifier`, `GetDirectoryEntryByIdentifier` | 30 s | 2 min |
| `GetUserParents`, `GetGroupParents` | 1 min | 5 min |
| Not found (404, or `null` on the detail queries) | 10 s | 30 s |

- Authentication, server profiles and catalog types are **not** cached (credentials/account state and in-memory configuration).
- Filters on `userPassword` / `unicodePwd` bypass the cache.
- Cache keys are deterministic and hashed (SHA-256); directory values never appear in Redis keys or logs. Search results larger than 200 entries stay in L1 only to bound Redis memory (in L2-only mode they are not cached).
- A successful create/disable/delete/set-password invalidates every cached read of its server profile before the response is sent.
- LDAP failures (`5xx`/`502`) are never cached.

### Staleness

The invalidation is best-effort because the gateway has no local transaction/outbox: a crash between the LDAP write and the invalidation, or an unavailable Redis, leaves stale entries until the TTL expires. Changes made directly in Active Directory by another actor cannot be invalidated at all; the L2 TTL (2 min for directory lookups) is the upper bound. Across instances, a lost backplane notification is repaired by the reconciler (about 60 s) or bounded by the L1 duration.

### Configuration

The `Caching` section in `src/Api/appsettings.json` holds the non-secret settings (`MemoryCacheEnabled`, `DistributedCacheEnabled`, `KeyPrefix`, `ChannelPrefix`, durations, timeouts). The Redis connection string is **required** when `DistributedCacheEnabled` is `true` and must not be committed:

```
Caching__RedisConnectionString=<host>:6380,ssl=true,abortConnect=false
```

For local development:

```
dotnet user-secrets set "Caching:RedisConnectionString" "localhost:6379,abortConnect=false" --project src/Api
```

#### Cache levels

L1 and L2 are enabled independently:

| `MemoryCacheEnabled` (L1) | `DistributedCacheEnabled` (L2) | Mode | Redis | Invalidation |
|---|---|---|---|---|
| `true` | `true` | L1 + L2 (default) | required | Redis generation + backplane + reconciler (all nodes) |
| `true` | `false` | L1 only | not used | local to the node; other nodes converge by TTL |
| `false` | `true` | L2 only | required | Redis is the single source of truth; it still publishes the generation and backplane notification so L1+L2 nodes sharing the prefix stay coherent. No reconciler (there is no L1 to repair) |
| `false` | `false` | No cache | not used | nothing to invalidate; every read goes to LDAP |

Environment variables: `Caching__MemoryCacheEnabled`, `Caching__DistributedCacheEnabled`. `MemoryCacheDuration` must not exceed `Duration` only when both levels are enabled. The reconciler and the `/health/ready` cache check exist only in the L1 + L2 mode.

**Breaking change:** `Caching:Enabled` was removed. Deployments that set it (`Caching__Enabled`) must switch to the two flags above: the old key is ignored and both levels default to `true`, which requires Redis.

If Redis is unavailable at runtime (and L2 is enabled), the service still starts and serves reads directly from LDAP with bounded timeouts.

Health endpoints: `/health/ready` (includes the cache reconciler and stays out of rotation until the generation subscription exists) and `/health/live` (process only, never depends on Redis).

### Redis security

- Use TLS and an ACL user limited to the commands needed: `SUBSCRIBE`/`PUBLISH` on the backplane and generation channels, `INCR`/`GET` on the generation key, and the key patterns under `KeyPrefix`.
- The physical L2 keys are prefixed by FusionCache with its protocol version: cached entries and tag markers look like `v2:<KeyPrefix>...`, while the generation key is `<ChannelPrefix>:cache:generation`. Grant both patterns. The backplane channel is `<ChannelPrefix>.Backplane:v2`; confirm the channel names with `PUBSUB CHANNELS` on the deployed version.
- The `maxmemory-policy` must not evict the generation key: use a `volatile-*` policy (all cached entries have a TTL) or a dedicated Redis instance.
- Use a distinct `KeyPrefix` and `ChannelPrefix` per service and environment; bump the prefix version (`v2:`) when a cached shape changes.

## Security follow-ups (manual)

The previous passwords remain in the git history and must be treated as compromised:

1. Rotate the bind account passwords of every profile that was ever committed, and any others used in `appsettings.Development.json`.
2. Remove the old values from the git history (for example with `git filter-repo`) and force-push, then ask contributors to re-clone.
3. Review existing clones, forks and CI caches.
4. Do not commit documents that quote credentials (for example code review notes).
