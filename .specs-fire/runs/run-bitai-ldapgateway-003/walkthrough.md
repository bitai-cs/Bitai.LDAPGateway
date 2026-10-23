---
run: run-bitai-ldapgateway-003
work_item: swagger-ui-oauth-tests
intent: swagger-ui-oauth-integration
generated: 2026-10-23T03:00:00Z
mode: confirm
---

# Implementation Walkthrough: Tests for Swagger UI OAuth integration

## Summary

Added functional tests that prove the Swagger UI OAuth behavior in both security modes and the startup validation of the `SwaggerUIApiClient` section. 7 new tests; the whole solution passes (Api.FunctionalTests 22/22).

## Structure Overview

`SwaggerUiOAuthTests` reuses `OAuthSecurityWebApplicationFactory` and creates a derived host per test in the `Testing` environment. Per-test settings are applied as environment variables around host creation and restored afterwards. Document tests fetch `/swagger/Bitai.LDAPWebApi/swagger.json` and inspect it with `System.Text.Json`; validation tests assert that host creation throws with the expected message.

## Files Changed

### Created

| File | Purpose |
|------|---------|
| `tests/Api.FunctionalTests/SwaggerUiOAuthTests.cs` | 7 tests plus a non-parallel xUnit collection definition |

## Key Implementation Details

### 1. Why environment variables
The API reads configuration while registering services (`AddApiSecurity`), so overrides must exist before that. `ConfigureAppConfiguration` is applied too late (validators were not registered), and `UseSetting` is turned into command-line args that `RunJasperFxCommands` rejects with "Unknown argument". Environment variables are read by `WebApplication.CreateBuilder` in time.

### 2. Isolation
The class runs in a collection with `DisableParallelization = true`, and variables are restored in `finally`, so other test classes never see them.

## Security Considerations

| Concern | Approach |
|---------|----------|
| Secrets in tests | Only dummy values (`test-secret`); no real credentials |

## Deviations from Plan

The planned "missing scopes list" case was replaced by blank-scope-name cases: the list originates in `appsettings.json` and cannot be emptied through environment variables.

## Developer Notes

- Test settings keys use `:` in code and are converted to `__` for environment variables.
- Earlier `OAuthSecurityTests` failures vanished when `appsettings.Development.json` was changed outside FIRE; they are not related to this work.

## How to Verify

`dotnet test Bitai.LDAPGateway.slnx` (expect all green; Api.FunctionalTests 22 tests).

## Ready for Review

- [x] Acceptance criteria met
- [x] Full test suite green
- [x] Build clean
- [x] No new dependencies
