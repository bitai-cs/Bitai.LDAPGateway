---
id: endpoint-authorization-metadata-test-hardening
title: Endpoint Authorization Metadata Test Hardening
status: in_progress
created: 2026-10-23T22:50:41Z
---

# Intent: Endpoint Authorization Metadata Test Hardening

## Goal

Make the functional authorization-metadata guard test accurately reject all routable MVC controller actions that could bypass an explicit named authorization policy.

## Users

API maintainers and reviewers who rely on the functional test suite to prevent accidental authorization regressions.

## Problem

The current guard treats any nonblank `IAuthorizeData.Policy` as sufficient even when `IAllowAnonymous` is effective, despite ASP.NET Core bypassing authorization in that case. It also discovers only actions bearing `HttpMethodAttribute`, leaving routable MVC actions with `[Route]` but no HTTP-method attribute outside the guard.

## Success Criteria

- `EveryControllerEndpoint_HasAnExplicitAuthorizationPolicy` fails for a routable controller action when `IAllowAnonymous` is present in the action's effective endpoint metadata, even if a named `IAuthorizeData.Policy` is also present.
- Discovery includes routable MVC controller actions decorated with `[Route]` but without an `HttpMethodAttribute`.
- Every discovered routable controller action is required to have an explicit, nonblank named authorization policy and no effective anonymous-authorization metadata.
- The test remains scoped to MVC controller endpoints and does not introduce application behavior changes.

## Constraints

- Limit changes to the functional authorization-metadata guard and its directly related test support, if needed.
- Follow the project's xUnit functional-test conventions and run `dotnet test Bitai.LDAPGateway.slnx` for verification.
- Do not implement application or production-code changes as part of this intent.

## Notes

The source review findings are the accepted requirements for this narrowly scoped regression-prevention change. ASP.NET Core treats `AllowAnonymous` as an authorization bypass, and route-only MVC actions must not be omitted from the policy guard.
