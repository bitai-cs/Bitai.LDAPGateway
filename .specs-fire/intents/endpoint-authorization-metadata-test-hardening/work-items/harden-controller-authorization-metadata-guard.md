---
id: harden-controller-authorization-metadata-guard
title: Harden controller authorization metadata guard
intent: endpoint-authorization-metadata-test-hardening
complexity: medium
mode: confirm
status: pending
depends_on: []
created: 2026-10-23T22:50:41Z
---

# Work Item: Harden controller authorization metadata guard

## Description

Update `EndpointAuthorizationMetadataTests` so its controller-action discovery covers MVC actions that are routable through either HTTP-method or route metadata, and so its authorization check rejects effective anonymous access rather than accepting a named policy alone.

## Acceptance Criteria

- [x] The guard identifies a controller action as routable when it has route metadata but no `HttpMethodAttribute`.
- [x] The guard identifies an action as unprotected when `IAllowAnonymous` is effective for that action, including when a nonblank named `IAuthorizeData.Policy` is also effective.
- [x] A discovered action passes only when it has an explicit, nonblank named authorization policy and no effective anonymous metadata.
- [x] The guard continues to report unprotected controller actions by declaring type and method name.
- [x] No application behavior or production source is changed.
- [x] `dotnet test Bitai.LDAPGateway.slnx` passes after the test change.

## Technical Notes

Use MVC's applicable controller/action metadata semantics, not only a method-local `HttpMethodAttribute` filter. Account for metadata that may be declared on a controller or action when determining routability and authorization effectiveness. Keep the test focused on `ControllerBase` actions.

## Dependencies

(none)
