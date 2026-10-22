# Data Model: OAuth API Security

## AuthorityConfiguration

| Field | Source | Required when bypass is disabled | Validation |
|-------|--------|----------------------------------|------------|
| `Authority` | `AuthorityConfiguration:Authority` | Yes | Absolute URI; no blank value. |
| `ApiResource` | `AuthorityConfiguration:ApiResource` | Yes | Non-blank; becomes the accepted access-token audience. |
| `RequireHttpsMetadata` | `AuthorityConfiguration:RequireHttpsMetadata` | Yes | Boolean; requires secure authority metadata retrieval when true. |

## ApiScopesConfiguration

| Field | Source | Required | Validation |
|-------|--------|----------|------------|
| `BypassApiScopesAuthorization` | `ApiScopesConfiguration:BypassApiScopesAuthorization` | Always | Boolean; defaults to `false` when omitted. |
| `AdminApiScopeName` | `ApiScopesConfiguration:AdminApiScopeName` | Only when bypass is disabled | Non-blank and different from reader scope. |
| `ReaderApiScopeName` | `ApiScopesConfiguration:ReaderApiScopeName` | Only when bypass is disabled | Non-blank and different from administrator scope. |

## Caller Credential

| Attribute | Meaning | Standard-mode rule |
|-----------|---------|--------------------|
| Issuer | Duende IdentityServer that issued the token | Must match configured authority. |
| Audience | API resource intended to consume the token | Must contain configured `ApiResource`. |
| Expiry | Access-token validity endpoint | Must not be expired. |
| Scopes | API permissions granted by Duende | Must contain administrator scope, or reader scope for reader-accessible endpoints. |

## Security Mode

| State | Entry condition | Effect |
|-------|-----------------|--------|
| Standard | Bypass is false or absent | Validate authority/resource/scopes; require administrator or reader policy. |
| Bypass | Bypass is true | Permit all callers; authority/resource/scope configuration may be absent; retain endpoint input validation and business rules. |

## Endpoint Classification

| Category | Required access |
|----------|-----------------|
| Any API endpoint | Administrator scope in standard mode. |
| Any HTTP GET endpoint | Administrator or reader scope in standard mode. |
| `POST .../Authentications/authenticate` | Administrator or reader scope in standard mode. |
| `POST .../Authentications/authenticateWithoutUserLookup` | Administrator or reader scope in standard mode. |
| All other non-GET endpoints | Administrator scope in standard mode. |

No new persistence, entity lifecycle, or identity-data ownership is introduced.
