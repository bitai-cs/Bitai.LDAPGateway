---
id: swagger-ui-client-options
title: Swagger UI client options and configuration
intent: swagger-ui-oauth-integration
complexity: low
mode: autopilot
status: completed
depends_on: []
created: 2026-10-22T00:00:00Z
---

# Work Item: Swagger UI client options and configuration

## Description

Create the options class bound to the new `SwaggerUIApiClient` section and add the section to `appsettings.json`. Register the binding with startup validation that applies only when API security is active (`BypassApiScopesAuthorization = false`).

## Acceptance Criteria

- [ ] `SwaggerUIApiClientOptions` exists in `src/Api/Options/` with `SwaggerUIClientId`, `SwaggerUIClientSecret` and `SwaggerUITargetApiScopes` (each item: `SwaggerUITargetApiScopeName`, `SwaggerUITargetApiScopeTitle`), plus a `SectionName` constant, following the style of `ApiScopesConfiguration`.
- [ ] `appsettings.json` contains `SwaggerUIApiClient` with ClientId `Bitai.LDAPGateway.Local.Swagger.Client`, an empty `SwaggerUIClientSecret`, and the scopes `Bitai.LDAPGateway.Scope.Reader` ("BITAI LDAP Gateway Reader Scope") and `Bitai.LDAPGateway.Scope.Admin` ("BITAI LDAP Gateway Admin Scope").
- [ ] Options are bound and validated on start only when security is active: ClientId, secret and at least one scope (non-blank name) are required.
- [ ] With `BypassApiScopesAuthorization = true`, a missing or empty section does not fail startup.
- [ ] Project builds with no new warnings.

## Technical Notes

Follow the `AddOptions<T>().BindConfiguration().Validate().ValidateOnStart()` pattern in `AddApiSecurity` (`src/Api/Extensions/ServiceCollectionExtensions.cs`). The secret value must not be committed; real value comes from user-secrets or environment variables.

## Dependencies

(none)
