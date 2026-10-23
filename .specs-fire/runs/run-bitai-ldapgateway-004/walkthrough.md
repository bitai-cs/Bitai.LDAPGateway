---
run: run-bitai-ldapgateway-004
work_item: harden-controller-authorization-metadata-guard
intent: endpoint-authorization-metadata-test-hardening
generated: 2026-10-23T23:10:00Z
mode: confirm
---

# Implementation Walkthrough: Harden controller authorization metadata guard

## Summary

The functional authorization guard now evaluates controller and action metadata together. It includes route-only MVC actions and fails any action that has anonymous-access metadata, even if a named authorization policy is also present.

## Structure Overview

The existing reflection-based functional test remains the single guard for API controller authorization metadata. No production code, endpoint behavior, or application architecture changed.

## Files Changed

### Created

| File | Purpose |
|------|---------|
| (none) | |

### Modified

| File | Changes |
|------|---------|
| `tests/Api.FunctionalTests/EndpointAuthorizationMetadataTests.cs` | Combines controller/action metadata, recognizes both HTTP-method and route-template actions, and treats `IAllowAnonymous` as unprotected access. |

## Key Implementation Details

### Effective metadata evaluation

The test combines metadata from the declaring controller and the action before classifying routability and authorization. This reflects controller-level policies, controller-level routes, and action-level overrides in the same guard.

### Route-only action coverage

Routability now accepts either MVC HTTP-method metadata or route-template metadata. A `[Route]` action without an HTTP-method attribute therefore remains under authorization-policy enforcement.

## Security Considerations

| Concern | Approach |
|---------|----------|
| Anonymous authorization bypass | Any effective `IAllowAnonymous` metadata makes the action fail the guard, regardless of named authorization policy metadata. |
| Missing explicit policy | The guard still requires a nonblank named `IAuthorizeData.Policy`. |

## Decisions Made

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Metadata source | Combine controller and action attributes | Policies, routes, and anonymous overrides can be declared at either level. |
| Routability test | MVC metadata interfaces | Covers HTTP-method and route-template actions without relying on a concrete attribute alone. |

## Deviations from Plan

None.

## Dependencies Added

None.

## How to Verify

1. **Run the full solution test suite**

   ```powershell
   dotnet test Bitai.LDAPGateway.slnx --no-restore
   ```

   Expected: all tests pass, including `EndpointAuthorizationMetadataTests`.

## Test Coverage

- Tests added: 0
- Coverage: not collected
- Status: passing (74 tests)

## Ready for Review

- [x] All acceptance criteria met
- [x] Tests passing
- [x] No critical issues
- [x] Documentation updated where required by FIRE
- [x] Developer notes captured

## Developer Notes

`IActionHttpMethodProvider` is the MVC abstraction for HTTP-method action metadata; `IRouteTemplateProvider` covers route-only metadata. Do not replace these checks with `HttpMethodAttribute` alone, or route-only actions will evade the guard.
