# Implementation Plan: Directory Review Remediation

**Branch**: `002-directory-review-remediation` | **Date**: 2026-10-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/002-directory-review-remediation/spec.md`

## Summary

Correct directory-search validation so every defined filter attribute is accepted and
undefined values and incomplete secondary-filter combinations are rejected. Restore
focused application unit coverage for search, lookup, and provisioning handlers using
the existing mock connector and domain-event publisher pattern. No public route,
request-field, persistence, or directory-connector contract changes are required.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: FluentValidation 12.1, WolverineFx 6.44, xUnit 2.9, Moq 4.20

**Storage**: N/A; the gateway remains stateless and the directory service remains the source of truth

**Testing**: xUnit with Moq in `tests/Application.UnitTests`

**Target Platform**: Cross-platform ASP.NET Core service

**Project Type**: REST web service with Clean Architecture layers

**Performance Goals**: Validation adds no directory I/O; invalid requests are rejected before connector invocation

**Constraints**: Preserve existing public request shape and explicit result semantics; publish exactly one completion event per covered operation; do not edit generated Wolverine handler code

**Scale/Scope**: One query validator and three focused application unit-test files covering search, single-entry lookup, and Microsoft Active Directory user provisioning boundaries

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Plan evidence |
|-----------|--------|---------------|
| I. Stateless, Directory-Neutral Gateway | PASS | No identity data or configuration is added. Validation runs before the existing directory connector and applies only to the neutral directory search path. |
| II. Clean Architecture and Explicit Use Cases | PASS | The change remains in the Application validator and Application unit-test project. Existing handler, connector-port, and result boundaries are reused. |
| III. Intentional Domain Modelling | PASS | Existing request, result, event, and enum types express all needed concepts; no new model or persistence abstraction is needed. |
| IV. Stable, Safe API Contracts | PASS | Routes and request fields do not change. Invalid enum and incomplete filter inputs remain validation failures; existing explicit results and safe error mapping are retained. |
| V. Security by Design | PASS | Rejecting undefined input before connector access narrows the accepted input surface. Tests must not log or assert a real provisioning password. |
| Configuration and Operations | PASS | No configuration or runtime profile behavior changes. |
| Quality and Evolution | PASS | Mock-adapter boundary tests will cover every changed behavior; build and targeted tests are required before completion. |

**Pre-design gate result**: PASS. No exceptions or ADR are required.

## Project Structure

### Documentation (this feature)

```text
specs/002-directory-review-remediation/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
src/
├── Application/
│   ├── Common/
│   │   └── LdapHandlerBase.cs
│   └── Directory/
│       ├── Commands/CreateMsAdUser/CreateMsAdUserCommand.cs
│       └── Queries/
│           ├── GetDirectoryEntryByIdentifier/GetDirectoryEntryByIdentifierQuery.cs
│           ├── GetGroupByIdentifier/GetGroupByIdentifierQuery.cs
│           └── SearchDirectory/SearchDirectoryQuery.cs
├── Domain/
│   ├── Enums/
│   └── Events/OperationCompletedDomainEvent.cs
└── Api/
    └── Controllers/DirectoryController.cs

tests/
└── Application.UnitTests/
    ├── DirectoryTestSupport.cs
    ├── DirectoryCommandHandlersTests.cs
    └── DirectoryQueryHandlersTests.cs
```

**Structure Decision**: Preserve the existing Clean Architecture layout. Apply the
validation change in the query's Application-layer validator and add focused xUnit
tests in the existing Application unit-test project; no API controller, Domain type,
Infrastructure implementation, or generated source change is needed.

## Complexity Tracking

No constitution violations or complexity exceptions require tracking.

## Post-Design Constitution Check

| Principle | Status | Design confirmation |
|-----------|--------|---------------------|
| Stateless, directory-neutral gateway | PASS | `data-model.md` introduces no persisted entity or directory-product-specific behavior outside the existing provisioning use case. |
| Clean Architecture and explicit use cases | PASS | Design limits production edits to the Application validator and tests interact through existing ports. |
| Stable, safe API contracts | PASS | `contracts/directory-validation.md` records behavioral validation only; route, parameter, response, and authorization shapes are unchanged. |
| Security by Design | PASS | The contract rejects invalid filter attributes before directory access; the quickstart uses test-only placeholder secrets. |
| Quality and Evolution | PASS | `quickstart.md` requires isolated tests and a release build; the event helper asserts exactly one total publication. |

**Post-design gate result**: PASS.
