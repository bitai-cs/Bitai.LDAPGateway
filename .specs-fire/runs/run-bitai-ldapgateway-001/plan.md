# Implementation Plan: swagger-ui-client-options

Mode: autopilot (no checkpoint)

## Approach

Add an options class for the `SwaggerUIApiClient` section, mirroring `AuthorityConfiguration`/`ApiScopesConfiguration`. Register binding and startup validation in the security-active branch of `AddApiSecurity` (after the bypass early return), so a missing section never fails startup when bypass is enabled. Add the section to `appsettings.json` with an empty client secret.

## Files to Create

- `src/Api/Options/SwaggerUIApiClientOptions.cs` — options + scope item class

## Files to Modify

- `src/Api/Extensions/ServiceCollectionExtensions.cs` — register options with `ValidateOnStart` in the non-bypass branch
- `src/Api/appsettings.json` — add `SwaggerUIApiClient` section

## Tests

Boundary tests for startup validation are planned in work item `swagger-ui-oauth-tests`. This run verifies by build and existing test suite, plus a manual startup check with the section empty.
