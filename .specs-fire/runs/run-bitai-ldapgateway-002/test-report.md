# Test Report: swagger-ui-oauth-security-scheme

## Test Results

- `dotnet build src/Api` : succeeded, 0 warnings.
- `dotnet test Bitai.LDAPGateway.slnx`: Domain 1/1, Application 6/6, LDAPHelper 44/44, Api.FunctionalTests 10 passed / **5 failed**.
- The 5 failures are the same pre-existing `OAuthSecurityTests.StandardMode_Rejects*` failures seen before any change in this intent (see run-bitai-ldapgateway-001 test report). No new failures.
- No automated tests added here; they belong to `swagger-ui-oauth-tests`.

## Manual verification (API run with `--no-launch-profile`)

| Scenario | Result |
|----------|--------|
| Bypass true (Development) | OpenAPI has no security scheme or requirement; Swagger UI `usePkceWithAuthorizationCodeGrant: false` |
| Bypass false + secret supplied | `components.securitySchemes.oauth2` = authorizationCode, authorizationUrl `https://localhost:44310/connect/authorize`, tokenUrl `.../connect/token`, scopes Reader/Admin with titles; global `security: [{oauth2: []}]`; Swagger UI PKCE `true` |
| Bypass false + Swagger scope not matching API scopes | Startup fails with "Swagger UI target API scopes must match the configured administrator or reader API scope names." |

## Acceptance Criteria Validation

- [x] OAuth2 authorizationCode scheme derived from `Authority` and `SwaggerUIApiClient` scopes when bypass is false.
- [x] Global security requirement applied.
- [x] Swagger UI configured with client id, secret and PKCE (settings present; Authorize dialog flow against a real authority NOT exercised).
- [x] Bypass true: no scheme and no Authorize configuration.
- [ ] A real token obtained through Swagger UI accepted by the API: not verified (needs a live authority at `https://localhost:44310`).
- [x] Design doc approved before implementation.
- [x] Build with no new warnings.
