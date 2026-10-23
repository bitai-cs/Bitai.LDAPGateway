# Implementation Plan: swagger-ui-oauth-tests

Mode: confirm.

## Approach

Add functional tests (xUnit + `WebApplicationFactory<Program>`, no live directory or authority) in a new class `SwaggerUiOAuthTests`. A small helper builds a factory from the `Testing` environment with in-memory configuration overrides (applied after the appsettings files), so each test controls `ApiScopesConfiguration:BypassApiScopesAuthorization` and the `SwaggerUIApiClient` section explicitly, independent of what `appsettings.Development.json` contains.

Tests:
1. Security active: `GET /swagger/{documentName}/swagger.json` contains `components.securitySchemes.oauth2` (authorizationCode, URLs from the authority, both scopes with titles) and the global `security` requirement.
2. Bypass true: the document has no security schemes and no `security` requirement.
3. Security active, missing client id / missing secret / empty scopes: host creation fails with the matching validation message (theory).
4. Security active, scope not matching Admin/Reader scope names: host creation fails with the alignment message.
5. Bypass true with no `SwaggerUIApiClient` section: host starts and the document is served.

## Files to Create

- `tests/Api.FunctionalTests/SwaggerUiOAuthTests.cs`

## Files to Modify

(none)

## Tests

`dotnet test Bitai.LDAPGateway.slnx`; Api.FunctionalTests currently passes 15/15 (the earlier `OAuthSecurityTests` failures are gone since `appsettings.Development.json` now has bypass false and a dummy secret).
