# Test Report: swagger-ui-oauth-tests

## Test Results

Command: `dotnet test Bitai.LDAPGateway.slnx`

- Domain.UnitTests: 1 passed
- Application.UnitTests: 6 passed
- Bitai.LDAPHelper.Tests: 44 passed
- Api.FunctionalTests: 22 passed (15 existing + 7 new), 0 failed

New tests (`SwaggerUiOAuthTests`):

| Test | Verifies |
|------|----------|
| SecurityActive_DocumentDeclaresOAuth2AuthorizationCodeScheme | authorizationCode scheme, authorize/token URLs from the authority, both scopes with titles, global requirement |
| BypassEnabled_DocumentHasNoSecurity | no `security` and no `securitySchemes` |
| SecurityActive_InvalidSwaggerUiClientConfiguration_FailsStartup (4 cases) | blank client id, blank secret, blank scope name, scope not matching Admin/Reader: startup fails with the matching message |
| BypassEnabled_WithoutSwaggerUiClientSection_StartsAndServesDocument | host starts and serves the document with blank Swagger client settings |

## Acceptance Criteria Validation

- [x] Security-active document contains the OAuth2 authorization code scheme with the configured scopes.
- [x] Bypass document has no security scheme.
- [x] Startup fails when security is active and client id, secret or a scope name is missing/blank, or a scope does not match the API scopes.
- [x] Startup succeeds with bypass enabled and no Swagger client values.
- [x] All new and existing tests pass.

## Notes / Gaps

- The "no scopes at all" case (empty list) is not covered: the list comes from `appsettings.json` and cannot be emptied through environment variables. Blank scope names are covered.
- Settings are passed as environment variables because the API reads configuration during service registration and `UseSetting` becomes command-line args that JasperFx rejects (`Unknown argument`). The class sits in a non-parallel collection so the variables never leak into other tests.
- The earlier 5 `OAuthSecurityTests` failures no longer occur (they disappeared when `appsettings.Development.json` was changed outside this run).
