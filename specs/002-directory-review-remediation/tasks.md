---

description: "Implementation tasks for Directory Review Remediation"
---

# Tasks: Directory Review Remediation

**Input**: Design documents from `/specs/002-directory-review-remediation/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/directory-validation.md](./contracts/directory-validation.md), [quickstart.md](./quickstart.md)

**Tests**: Required. FR-007 explicitly requires automated boundary coverage using the existing xUnit and Moq application-test conventions.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the reusable mock fixture used by the directory handler tests.

- [X] T001 Create shared connector, publisher, request-context, result-error, and LDAP-entry builders in `tests/Application.UnitTests/DirectoryTestSupport.cs`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Make completion-event assertions detect both missing/mismatched events and unexpected extra events before story coverage is added.

**⚠️ CRITICAL**: Complete this phase before adding lookup or provisioning handler tests.

- [X] T002 Update `VerifyEvent` in `tests/Application.UnitTests/DirectoryTestSupport.cs` to verify exactly one total `IDomainEventPublisher.PublishAsync` call and then verify that event's operation name, server profile, catalog type, and success status.

**Checkpoint**: Shared test support enforces the one-event invariant for all later handler tests.

---

## Phase 3: User Story 1 - Search Directory Entries Reliably (Priority: P1) 🎯 MVP

**Goal**: Accept every defined primary directory attribute and reject undefined attributes or incomplete secondary-filter triples before a directory search.

**Independent Test**: Run the search-validator tests and confirm the zero-valued `c` attribute is valid, an undefined primary attribute is invalid, and every partial secondary-filter combination is invalid.

### Tests for User Story 1

- [X] T003 [US1] Add `SearchDirectoryQueryValidator` boundary tests in `tests/Application.UnitTests/DirectoryQueryHandlersTests.cs` for the rule “Must be a defined directory-entry attribute; a defined zero-valued attribute is valid,” plus undefined primary attributes.
- [X] T004 [US1] Add secondary-filter boundary tests in `tests/Application.UnitTests/DirectoryQueryHandlersTests.cs` for the rules “When present, the secondary value and combination choice are both required,” “Must be nonblank when a secondary attribute is present; otherwise must be absent,” and “Must be present when a secondary attribute is present; otherwise must be absent.”

### Implementation for User Story 1

- [X] T005 [P] [US1] Replace the primary filter-attribute presence rule with defined-enum validation and add the complete secondary-filter all-or-nothing rule set in `src/Application/Directory/Queries/SearchDirectory/SearchDirectoryQuery.cs`.

**Checkpoint**: Valid searches using the zero-valued attribute proceed through validation; undefined attributes and all partial secondary-filter triples are rejected without changing the route or query parameters.

---

## Phase 4: User Story 2 - Receive Accurate Directory Operation Outcomes (Priority: P2)

**Goal**: Preserve connector failures for group and directory-entry lookups and publish one matching failed completion event.

**Independent Test**: Simulate each connector failure and assert the original error result plus exactly one failed completion event with the requested profile and catalog.

### Tests for User Story 2

- [X] T006 [US2] Add group-lookup connector-failure coverage in `tests/Application.UnitTests/DirectoryQueryHandlersTests.cs` that asserts `GetGroupByIdentifierQueryHandler` returns the connector error unchanged and `DirectoryTestSupport.VerifyEvent` observes one failed `GetGroupByIdentifier` event.
- [X] T007 [US2] Add directory-entry lookup connector-failure coverage in `tests/Application.UnitTests/DirectoryQueryHandlersTests.cs` that asserts `GetDirectoryEntryByIdentifierHandler` returns the connector error unchanged and `DirectoryTestSupport.VerifyEvent` observes one failed `GetDirectoryEntryByIdentifier` event.

**Checkpoint**: Both lookup failure paths are independently verified with no live directory dependency.

---

## Phase 5: User Story 3 - Trust Provisioning Completion Results (Priority: P3)

**Goal**: Prove successful Active Directory user provisioning calls the connector with the original context and payload, returns its entry, and emits one matching success event.

**Independent Test**: Configure a unique returned entry, invoke the provisioning handler, and verify the connector call, returned entry identity, and exactly one successful completion event.

### Tests for User Story 3

- [X] T008 [P] [US3] Add the successful `CreateMsAdUserCommandHandler` boundary test in `tests/Application.UnitTests/DirectoryCommandHandlersTests.cs` that verifies the connector receives the expected request context and account payload, asserts the result is the connector-returned entry, and verifies one successful `CreateMsAdUser` event without exposing a real password.

**Checkpoint**: A fabricated successful provisioning result or connector bypass fails the test.

---

## Phase 6: Polish & Cross-Cutting Validation

**Purpose**: Validate the feature against its focused and repository-wide quality gates.

- [X] T009 Run the focused application unit suite from `tests/Application.UnitTests/Bitai.LDAPGateway.Application.UnitTests.csproj` and resolve failures.
- [X] T010 Run the release build for `Bitai.LDAPGateway.slnx` and resolve warnings or errors.
- [X] T011 Review `specs/002-directory-review-remediation/quickstart.md` against the implemented validation commands and update it only if the executable commands or expected outcomes changed.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on T001 and supplies the event assertion used by US2 and US3.
- **US1 (Phase 3)**: Can start after T001; T005 depends on T003 and T004.
- **US2 (Phase 4)**: Depends on T002; T006 and T007 modify the same test file and should be completed sequentially.
- **US3 (Phase 5)**: Depends on T002.
- **Polish (Phase 6)**: Depends on all desired user stories; T009 precedes T010 and T011.

### User Story Dependencies

- **US1 (P1)**: Independent after T001; delivers the MVP.
- **US2 (P2)**: Independent of US1 after T002.
- **US3 (P3)**: Independent of US1 and US2 after T002.

### Parallel Opportunities

- T003 and T004 can be designed together but modify the same file, so they are intentionally sequential.
- After T002, T006/T007 and T008 can proceed in parallel because they modify `DirectoryQueryHandlersTests.cs` and `DirectoryCommandHandlersTests.cs`, respectively.
- T005 and T008 can proceed in parallel after their prerequisite test designs are complete because they modify different files.

## Parallel Example: User Story 3 and User Story 2

```text
Task: "Add successful provisioning boundary coverage in tests/Application.UnitTests/DirectoryCommandHandlersTests.cs"
Task: "Add group and directory-entry connector-failure coverage in tests/Application.UnitTests/DirectoryQueryHandlersTests.cs"
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete T001.
2. Write T003 and T004, then make T005 pass them.
3. Run the focused validator tests in `tests/Application.UnitTests/DirectoryQueryHandlersTests.cs`.
4. Stop and validate the unchanged public search contract before continuing.

### Incremental Delivery

1. Deliver US1 to correct search validation.
2. Add US2 to protect failed lookup outcomes and completion-event status.
3. Add US3 to protect provisioning forwarding and returned-entry integrity.
4. Run T009 through T011 before considering the remediation complete.

## Notes

- Every task uses the required checklist format, sequential ID, and exact repository path.
- No task edits generated Wolverine handler source, API route definitions, persistence, or directory-connector interfaces.
- The historical `run-bitai-ldapgateway-010` record is outside the current branch and remains explicitly out of scope.
