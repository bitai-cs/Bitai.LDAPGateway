---
id: swagger-ui-oauth-tests
title: Tests for Swagger UI OAuth integration
intent: swagger-ui-oauth-integration
complexity: medium
mode: confirm
status: pending
depends_on: [swagger-ui-oauth-security-scheme]
created: 2026-10-22T00:00:00Z
---

# Work Item: Tests for Swagger UI OAuth integration

## Description

Add automated tests covering the Swagger UI OAuth behavior in both security modes and the startup validation of `SwaggerUIApiClient`.

## Acceptance Criteria

- [ ] A test with security active verifies the OpenAPI document contains the OAuth2 authorization code scheme with the configured scopes.
- [ ] A test with `BypassApiScopesAuthorization = true` verifies the document has no security scheme.
- [ ] Tests verify startup fails when security is active and ClientId, secret or scopes are missing.
- [ ] Tests verify startup succeeds with bypass enabled and no `SwaggerUIApiClient` section.
- [ ] All new and existing tests pass.

## Technical Notes

Existing functional tests are in `tests/Api.FunctionalTests`; `appsettings.Testing.json` already sets bypass to true, so security-active cases need configuration overrides. Follow the testing standards in `.specs-fire/standards/` and the project's existing test framework.

## Dependencies

- swagger-ui-oauth-security-scheme
