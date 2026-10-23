---
id: swagger-ui-oauth-integration
title: Swagger UI OAuth/OIDC Integration
status: completed
created: 2026-10-22T00:00:00Z
---

# Intent: Swagger UI OAuth/OIDC Integration

## Goal

Make Swagger UI (`/swagger`, NSwag.AspNetCore 14.7.1) in `Bitai.LDAPGateway.Api` aligned with the API's OAuth/OIDC security. When security is active, the user selects scopes and authenticates via authorization code against the configured authority, then tests endpoints with the obtained token.

## Users

Developers and integrators who explore and test the API from `/swagger`.

## Problem

With security active (`ApiScopesConfiguration:BypassApiScopesAuthorization = false`), Swagger UI cannot authenticate, so every endpoint called from the UI returns 401.

## Success Criteria

- With `BypassApiScopesAuthorization = false`, the OpenAPI document includes an OAuth2 authorization code scheme using `AuthorityConfiguration.Authority` and the scopes from `SwaggerUIApiClient`; Swagger UI shows the Authorize button with those scopes.
- With `BypassApiScopesAuthorization = true`, Swagger UI shows and requires no security.
- `SwaggerUIApiClient` is added to `appsettings.json` and bound to an options class:
  - `SwaggerUIClientId`: `Bitai.LDAPGateway.Local.Swagger.Client`
  - `SwaggerUITargetApiScopes`: `Bitai.LDAPGateway.Scope.Reader` ("BITAI LDAP Gateway Reader Scope") and `Bitai.LDAPGateway.Scope.Admin` ("BITAI LDAP Gateway Admin Scope"), each with `SwaggerUITargetApiScopeName` / `SwaggerUITargetApiScopeTitle`.
- The client secret is read from configuration via a new `SwaggerUIClientSecret` key; empty in `appsettings.json`, real value from user-secrets or environment variables.
- The section is validated on startup only when security is active: ClientId, secret and at least one scope are required.
- Tests cover both modes (bypass and security active).

## Constraints

- A client secret in Swagger UI reaches the browser; use only in controlled environments (Local/Dev). Document this.
- Swagger scope names must stay aligned with `ApiScopesConfiguration` (`AdminApiScopeName` / `ReaderApiScopeName`).
- Follow existing style: `ServiceCollectionExtensions`, `ApplicationBuilderExtensions`, `src/Api/Options/`.

## Notes

Current state: `BypassApiScopesAuthorization = true` disables security (allow-all policy, no JWT Bearer); security is active only when `false`. Swagger UI currently only calls `UseOpenApi()` / `UseSwaggerUi()` with no security settings (`src/Api/Extensions/ApplicationBuilderExtensions.cs`). OpenAPI is registered in `AddWebApiConfiguration` (`src/Api/Extensions/ServiceCollectionExtensions.cs`).
