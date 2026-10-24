---
id: uniform-domain-events-for-profile-handlers
title: Uniform domain events for profile and catalog handlers
intent: code-review-20251023-remediation
complexity: medium
mode: confirm
status: completed
depends_on: []
created: 2026-10-24T00:00:00Z
run_id: run-bitai-ldapgateway-007
completed_at: 2026-10-24T01:47:41.371Z
---

# Work Item: Uniform domain events for profile and catalog handlers

## Description

Make GetProfileById, GetProfileIds, GetProfiles and GetCatalogTypes handlers inherit `LdapHandlerBase` and publish `OperationCompletedDomainEvent` (review M1), and stop `WolverineDomainEventPublisher` from silently dropping unknown events (review M5).

## Acceptance Criteria

- [ ] All four handlers inherit `LdapHandlerBase` and publish the event on success and failure.
- [ ] Unknown `IDomainEvent` types are not silently ignored (explicit failure or warning log).
- [ ] Tests assert the event is published for each handler and for the unknown-event behavior.
- [ ] Public responses of the affected endpoints are unchanged.
- [ ] `dotnet test Bitai.LDAPGateway.slnx` passes.

## Technical Notes

These handlers do not touch LDAP, so a request context with the relevant profile/catalog (or a documented neutral value) is needed for `ExecuteAsync`. For unknown events, fail explicitly or log a warning.

## Dependencies

(none)
