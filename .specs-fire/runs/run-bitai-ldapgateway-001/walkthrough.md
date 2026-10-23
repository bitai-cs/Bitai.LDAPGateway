---
run: run-bitai-ldapgateway-001
work_item: swagger-ui-client-options
intent: swagger-ui-oauth-integration
generated: 2026-10-23T01:31:00Z
mode: autopilot
---

# Implementation Walkthrough: Swagger UI client options and configuration

## Summary

Added a typed options class for the new `SwaggerUIApiClient` configuration section and the section itself in `appsettings.json`. The options are bound and validated on startup only when API security is active, so bypass mode never requires them.

## Structure Overview

`SwaggerUIApiClientOptions` (client id, client secret, list of target scopes with name and title) lives next to the other options in `src/Api/Options/`. `AddApiSecurity` registers it in the security-active branch, after the bypass early return, alongside the existing `AuthorityConfiguration` registration. The next work item will consume these options to build the OpenAPI security scheme and configure Swagger UI.

## Files Changed

### Created

| File | Purpose |
|------|---------|
| `src/Api/Options/SwaggerUIApiClientOptions.cs` | Options for the `SwaggerUIApiClient` section, plus the scope item type |

### Modified

| File | Changes |
|------|---------|
| `src/Api/Extensions/ServiceCollectionExtensions.cs` | Bind and validate `SwaggerUIApiClientOptions` with `ValidateOnStart` when security is active |
| `src/Api/appsettings.json` | Added `SwaggerUIApiClient` with the agreed client id, empty secret and Reader/Admin scopes |

## Key Implementation Details

### 1. Validation only in the security-active branch

Registering the options after the bypass `return` means bypass mode (Development, Testing) never validates or even binds the section, matching the requirement that a missing section must not fail startup there.

## Security Considerations

| Concern | Approach |
|---------|----------|
| Client secret | Empty in `appsettings.json`; real value via user-secrets or environment variables. Validation messages never echo values. |
| Secret in browser | Swagger UI exposes the secret to the browser; use only in controlled environments (to be addressed in the next work item's design). |

## Deviations from Plan

None.

## Developer Notes

- With the default `appsettings.json` (bypass false), the app now fails at startup until `SwaggerUIApiClient:SwaggerUIClientSecret` is provided. Example: `dotnet user-secrets set "SwaggerUIApiClient:SwaggerUIClientSecret" "<secret>" --project src/Api` (or env var `SwaggerUIApiClient__SwaggerUIClientSecret`).
- 5 `OAuthSecurityTests.StandardMode_Rejects*` tests fail both before and after this change (pre-existing); see `test-report.md`.
- Scope names should stay aligned with `ApiScopesConfiguration`; a cross-check is a candidate for the next work item.

## How to Verify

1. `dotnet build Bitai.LDAPGateway.slnx` — no warnings.
2. Run the API with bypass false and no secret: startup fails with the "Swagger UI client secret is required" message.
3. Set the secret and run again: startup succeeds. With bypass true, no section is needed.

## Ready for Review

- [x] Acceptance criteria met
- [x] Build clean
- [ ] Full test suite green (5 pre-existing failures, unrelated)
- [x] No new dependencies
