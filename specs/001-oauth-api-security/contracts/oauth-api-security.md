# OAuth API Security Contract

## Authority reference

The gateway trusts access tokens issued by the configured Duende IdentityServer authority. In standard security mode, callers send an access token in:

```http
Authorization: Bearer <access-token>
```

The token must be issued by `AuthorityConfiguration:Authority`, target `AuthorityConfiguration:ApiResource`, be current, and contain the appropriate Duende API scope.

## Configuration

```json
{
  "ApiScopesConfiguration": {
    "BypassApiScopesAuthorization": false,
    "AdminApiScopeName": "Bitai.LDAPGateway.Scope.Admin",
    "ReaderApiScopeName": "Bitai.LDAPGateway.Scope.Reader"
  },
  "AuthorityConfiguration": {
    "Authority": "https://localhost:44310",
    "ApiResource": "Bitai.LDAPGateway.Resource",
    "RequireHttpsMetadata": true
  }
}
```

When `BypassApiScopesAuthorization` is omitted, it is `false`. When it is `true`, no authority, resource, metadata, or scope configuration is required and every API endpoint permits unauthenticated callers.

## Access matrix

| Endpoint category | Administrator scope | Reader scope | No/invalid credential with bypass disabled | Any caller with bypass enabled |
|-------------------|---------------------|--------------|--------------------------------------------|-------------------------------|
| Any endpoint | Allowed | Depends on category | Denied | Allowed |
| HTTP GET endpoint | Allowed | Allowed | Denied | Allowed |
| `POST /api/{serverProfile}/{catalogType}/Authentications/authenticate` | Allowed | Allowed | Denied | Allowed |
| `POST /api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup` | Allowed | Allowed | Denied | Allowed |
| Other non-GET endpoint | Allowed | Denied | Denied | Allowed |

## Failure contract

In standard mode, absent, malformed, expired, wrong-authority, wrong-resource, or insufficient-scope credentials produce the API's established safe authorization failure response. Responses must not expose credentials, signing keys, or authority internals. Existing endpoint validation and problem-details behavior remains unchanged.

## OpenAPI contract

The published OpenAPI document must describe bearer security for protected standard-mode endpoints and identify which operations use administrator versus reader-or-administrator access. It must not expose secrets or environment-specific authority credentials.
