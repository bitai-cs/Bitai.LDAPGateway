---
id: swagger-ui-oauth-security-scheme
title: OpenAPI OAuth2 authorization code scheme and Swagger UI settings
intent: swagger-ui-oauth-integration
complexity: high
mode: validate
status: completed
depends_on: [swagger-ui-client-options]
created: 2026-10-22T00:00:00Z
---

# Work Item: OpenAPI OAuth2 authorization code scheme and Swagger UI settings

## Description

When API security is active, add an OAuth2 authorization code security scheme to the NSwag OpenAPI document and configure Swagger UI with the client id and client secret, so the Authorize button lets the user pick scopes and complete the authorization code flow against the configured authority. When security is bypassed, add no security to the document or UI.

## Acceptance Criteria

- [ ] With `BypassApiScopesAuthorization = false`, the OpenAPI document defines an OAuth2 `authorizationCode` scheme whose authorization and token URLs are derived from `AuthorityConfiguration.Authority`, and whose scopes are those in `SwaggerUIApiClient` (name and title).
- [ ] The document applies a security requirement with those scopes to the operations.
- [ ] Swagger UI is configured with `SwaggerUIClientId` and `SwaggerUIClientSecret`, and the Authorize dialog lists the configured scopes.
- [ ] With `BypassApiScopesAuthorization = true`, the document has no security scheme and Swagger UI shows no Authorize button.
- [ ] A token obtained through Swagger UI is accepted by the API for endpoints requiring the Reader or Admin scope.
- [ ] A design doc is approved before implementation (validate mode), covering authority endpoint URLs (discovery vs. standard paths), redirect URI and the client-secret-in-browser risk.
- [ ] Project builds with no new warnings.

## Technical Notes

OpenAPI registration is in `AddWebApiConfiguration`; Swagger UI is set up in `UseSwaggerUiIfConfigured` (`src/Api/Extensions/`). Use NSwag 14.7.1 APIs (`AddSecurity`, `OperationSecurityScopeProcessor` or equivalent, `SwaggerUiSettings.OAuth2Client`). A client secret in Swagger UI reaches the browser; restrict to controlled environments and document this. Swagger scope names must stay aligned with `ApiScopesConfiguration`.

## Dependencies

- swagger-ui-client-options
