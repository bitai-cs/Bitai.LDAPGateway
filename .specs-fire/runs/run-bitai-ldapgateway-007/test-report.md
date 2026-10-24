---
run: run-bitai-ldapgateway-007
work_item: uniform-domain-events-for-profile-handlers
intent: code-review-20251023-remediation
---

# Test Report: Uniform domain events for profile and catalog handlers

`dotnet test Bitai.LDAPGateway.slnx`: all passing. `dotnet build src/Api -c Release` (uses the committed pre-generated code): succeeds.

| Project | Passed | Failed |
|---|---|---|
| Domain.UnitTests | 1 | 0 |
| Application.UnitTests | 24 (5 new) | 0 |
| Api.FunctionalTests | 24 (1 new) | 0 |
| Bitai.LDAPHelper.Tests (external) | 44 | 0 |

## Acceptance Criteria Validation

- [x] All four handlers inherit `LdapHandlerBase` and publish the event on success and failure (GetProfileById tested for both).
- [x] Unknown `IDomainEvent` types are not silently ignored: a warning naming the type is logged (`UnknownDomainEvent_IsNotSilentlyDropped`).
- [x] Tests assert the event is published for each handler.
- [x] Public responses unchanged: existing functional tests for `/api/CatalogTypes` and `/api/ServerProfiles/...` pass.
- [x] `dotnet test Bitai.LDAPGateway.slnx` passes.

## Notes

Build warnings are only the pre-existing ones from the external `Bitai.LDAPHelper` / `Bitai.LDAPWebApi` projects, not from this repository.
