---
run: run-bitai-ldapgateway-004
work_item: harden-controller-authorization-metadata-guard
intent: endpoint-authorization-metadata-test-hardening
mode: confirm
checkpoint: plan
approved_at: null
---

# Implementation Plan: Harden controller authorization metadata guard

## Approach

Refactor the functional guard's reflection predicate so it evaluates each public, declared MVC controller action with its effective controller-and-action metadata. Treat an action as routable when either level supplies HTTP-method or route-template metadata. Treat an action as protected only when the combined metadata contains a nonblank named authorization policy and contains no `IAllowAnonymous` metadata. Continue emitting `Controller.Action` names for any failures.

Keep the change confined to the functional test project; no application behavior or production source will change.

## Files to Create

| File | Purpose |
|------|---------|
| (none) | |

## Files to Modify

| File | Changes |
|------|---------|
| `tests/Api.FunctionalTests/EndpointAuthorizationMetadataTests.cs` | Discover route-only controller actions, combine controller/action authorization metadata, and reject effective anonymous access even when a named policy exists. |

## Tests

| Test File | Coverage |
|-----------|----------|
| `tests/Api.FunctionalTests/EndpointAuthorizationMetadataTests.cs` | The functional guard validates all routable controller actions have a named policy and no anonymous override. |

## Technical Details

- Use MVC metadata interfaces rather than assuming every routable action is decorated with `HttpMethodAttribute`.
- Preserve the existing scan scope: non-abstract `ControllerBase` subclasses and public declared instance methods.
- Validate with `dotnet test Bitai.LDAPGateway.slnx`.

---
*Plan approval is required before execution.*
