---
run: run-bitai-ldapgateway-002
work_item: swagger-ui-oauth-security-scheme
intent: swagger-ui-oauth-integration
generated: 2026-10-23T02:05:00Z
mode: validate
---

# Implementation Walkthrough: OpenAPI OAuth2 authorization code scheme and Swagger UI settings

## Summary

When API security is active (`BypassApiScopesAuthorization = false`), the OpenAPI document now declares an OAuth2 authorization code scheme and Swagger UI is configured with the client id, client secret and PKCE, so the Authorize dialog lists the configured scopes. In bypass mode nothing changes. Startup also validates that Swagger scopes match the API's Admin/Reader scopes.

## Structure Overview

`AddWebApiConfiguration` registers the OpenAPI document with a service-provider-aware callback. That callback reads `ApiScopesConfiguration`, and when security is active adds the scheme using `AuthorityConfiguration` (URLs) and `SwaggerUIApiClientOptions` (scopes). `UseSwaggerUiIfConfigured` sets `OAuth2Client` from the same options. `AddApiSecurity` gained one more validation on `SwaggerUIApiClientOptions` (scope alignment).

## Files Changed

### Modified

| File | Changes |
|------|---------|
| `src/Api/Extensions/ServiceCollectionExtensions.cs` | OAuth2 scheme and global requirement in `AddOpenApiDocument`; scope-alignment validation; `SwaggerOAuth2SchemeName` constant |
| `src/Api/Extensions/ApplicationBuilderExtensions.cs` | `OAuth2Client` (client id, secret, PKCE) in `UseSwaggerUi` when security is active |

## Key Implementation Details

### 1. Endpoints derived from the authority
Authorize and token URLs are `{Authority}/connect/authorize` and `/connect/token` (IdentityServer/Duende convention), with no network call at startup.

### 2. Global requirement
`AddSecurity(name, emptyScopes, scheme)` adds the scheme and a global requirement, so every operation shows the lock; the user chooses scopes in the dialog.

## Security Considerations

| Concern | Approach |
|---------|----------|
| Client secret in browser | Reaches the browser via Swagger UI settings; use only in Local/Dev; supply via user-secrets/env vars; mitigated partly by PKCE |
| Scope drift | Startup validation: Swagger scopes must equal Admin or Reader scope names |
| Bypass mode | No scheme, no OAuth settings, options not resolved |

## Deviations from Plan

None. (`AddSecurity` was switched to the 3-argument overload so the global requirement is emitted.)

## Developer Notes

- Register `https://<host>/swagger/oauth2-redirect.html` as a redirect URI for client `Bitai.LDAPGateway.Local.Swagger.Client` at the authority, and enable PKCE for it.
- `dotnet run` applies `launchSettings.json`; use `--no-launch-profile` to control environment and URLs.
- The 5 pre-existing `OAuthSecurityTests` failures are unrelated.
- The end-to-end login via Swagger UI was not exercised (no live authority).

## How to Verify

1. Run with bypass false and `SwaggerUIApiClient__SwaggerUIClientSecret` set; open `/swagger` and click Authorize: the Reader and Admin scopes appear.
2. `GET /swagger/Bitai.LDAPWebApi/swagger.json` contains `components.securitySchemes.oauth2` and `security: [{oauth2: []}]`.
3. With bypass true, the document has no security and Swagger UI works unauthenticated.

## Ready for Review

- [x] Acceptance criteria met (except live login)
- [x] Build clean
- [ ] Full test suite green (5 pre-existing failures)
- [ ] End-to-end authorization against a real authority
- [x] No new dependencies
