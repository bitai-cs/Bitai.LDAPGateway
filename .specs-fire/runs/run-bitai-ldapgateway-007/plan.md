---
run: run-bitai-ldapgateway-007
work_item: uniform-domain-events-for-profile-handlers
intent: code-review-20251023-remediation
mode: confirm
checkpoint: plan
approved_at: 2026-10-24T01:40:00Z
---

# Implementation Plan: Uniform domain events for profile and catalog handlers

## Approach

**Design problem.** `OperationCompletedDomainEvent` and `LdapRequestContext` require a `ServerProfile` and a non-null `CatalogType`. The four handlers do not touch LDAP: `GetCatalogTypes`, `GetProfileIds` and `GetProfiles` have neither a profile nor a catalog; `GetProfileById` has a profile but no catalog. Passing fake values (e.g. catalog `LC`) would write false data into the audit trail.

**Proposed solution (keeps `LdapRequestContext` and the connector untouched):**

1. `OperationCompletedDomainEvent`: `ServerProfile` becomes `string?` and `CatalogType` becomes `CatalogType?`. Existing callers still compile (implicit conversions). The notification log prints empty values for absent fields.
2. `LdapHandlerBase`: add `ExecuteAsync<T>(operationName, serverProfile, operation, ct)` that publishes the event with `CatalogType = null`. Existing overloads are unchanged.
3. The four handlers inherit `LdapHandlerBase`, take `IDomainEventPublisher`, and wrap their body in the new overload. Results returned to clients are unchanged.
4. `WolverineDomainEventPublisher`: unknown `IDomainEvent` types are no longer swallowed; the publisher logs a warning naming the event type. (Alternative: throw `NotSupportedException`; I chose a warning so an audit hook can never fail a user request. Say so if you prefer throwing.)
5. **Regenerate Wolverine pre-generated handlers** (`dotnet run --project src/Api -- codegen write`) and commit the changed generated files, as the constitution requires.

## Files to Create

| File | Purpose |
|------|---------|
| `tests/Application.UnitTests/ProfileAndCatalogHandlersTests.cs` | Each of the four handlers publishes exactly one `OperationCompletedDomainEvent` (success; plus not-found for GetProfileById) |
| `tests/Infrastructure.UnitTests` (not created) | See note below |

## Files to Modify

| File | Changes |
|------|---------|
| `src/Domain/Events/OperationCompletedDomainEvent.cs` | `ServerProfile`, `CatalogType` nullable |
| `src/Application/Common/LdapHandlerBase.cs` | New `ExecuteAsync` overload without catalog |
| `src/Application/ServerProfiles/Queries/*/*.cs` (3) and `src/Application/CatalogTypes/Queries/GetCatalogTypes/GetCatalogTypesQuery.cs` | Inherit `LdapHandlerBase`, publish event |
| `src/Infrastructure/Services/WolverineDomainEventPublisher.cs` | Warn on unknown events (inject `ILogger`) |
| `src/Api/Internal/Generated/WolverineHandlers/*` | Regenerated |

## Tests

| Test File | Coverage |
|-----------|----------|
| `tests/Application.UnitTests/ProfileAndCatalogHandlersTests.cs` | Event published by the four handlers, with null catalog |
| `tests/Api.FunctionalTests/MigrationBehaviorTests.cs` | Unknown-event publisher test (the publisher lives in Infrastructure and is resolved from the app container; a unit test project for Infrastructure does not exist and I will not create one) |

## Technical Details

- Existing functional tests (`GET /api/CatalogTypes`, `/api/ServerProfiles/...`) guard that HTTP responses are unchanged.
- The notification handler logs `Catalog={Catalog}`; with null it prints empty. Acceptable and honest.
- Risk: the generated-code diff will be large-ish; I will review it and mention it in the report.

---
*Plan approved.*
