# Test Report: swagger-ui-client-options

## Test Results

Command: `dotnet test Bitai.LDAPGateway.slnx` (build clean, no warnings)

- Application.UnitTests: 6 passed
- Bitai.LDAPHelper.Tests: 44 passed
- Api.FunctionalTests: 10 passed, **5 failed**

The 5 failures are all in `OAuthSecurityTests` (`StandardMode_Rejects*`). They are **pre-existing**: running the same suite with this run's changes stashed gives the identical 5 failures (security-active mode returns 200/404 instead of 401/403). Not caused by this work item; they need separate investigation.

No new tests were added here; boundary tests for the new validation belong to `swagger-ui-oauth-tests`.

## Acceptance Criteria Validation

- [x] `SwaggerUIApiClientOptions` created in `src/Api/Options/` with the required properties and `SectionName`.
- [x] `appsettings.json` has `SwaggerUIApiClient` with the agreed ClientId, empty secret and two scopes.
- [x] Options bound and validated on start (client id, secret, at least one scope) only in the security-active branch.
- [x] With bypass enabled, the options are not registered, so a missing section cannot fail startup (Development/Testing tests that use bypass still pass).
- [x] Build has no new warnings.

## Notes

With the default `appsettings.json` (bypass false, empty secret) startup now fails until `SwaggerUIApiClient:SwaggerUIClientSecret` is supplied via user-secrets or environment variables. This is the intended validation.
