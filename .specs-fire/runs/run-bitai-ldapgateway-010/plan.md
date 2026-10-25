---
run: run-bitai-ldapgateway-010
work_item: use-case-boundary-tests
intent: code-review-20251023-remediation
mode: confirm
checkpoint: plan
checkpoint_state: approved
---

# Implementation Plan: Boundary tests for untested use cases

## Approach

Handler-level boundary tests with a Moq `IDirectoryServiceConnector` and `IDomainEventPublisher`, following the convention of `AuthenticateCommandHandlerTests`. No directory is needed and the tests have no dependency on Infrastructure.

Use cases already covered: Authenticate, SetMsAdUserPassword (command logging), profile and catalog handlers. To cover (12 handlers): CreateMsAdUser, SetMsAdUserPassword, DisableMsAdUser, DeleteMsAdUser, SearchDirectory, SearchUsers, SearchGroups, GetDirectoryEntryByIdentifier, GetUserByIdentifier, GetGroupByIdentifier, GetUserParents, GetGroupParents.

For each handler:
- Success: forwards the exact context (profile and catalog) and arguments to the connector, returns the connector result, publishes one `OperationCompletedDomainEvent` with the right operation name and `Success = true`.
- Failure: a failed connector result is returned unchanged and the event is published with `Success = false`.

Extra boundaries:
- Get user / group / entry: not found with `MustExist = true` returns `not_found`; with `false` returns success with a null value.
- Validators: empty `ServerProfile` or `Identifier` rejected for the write commands; invalid enum value rejected for queries; search second-filter all-or-nothing rule.

## Files to Create

| File | Purpose |
|------|---------|
| `tests/Application.UnitTests/DirectoryCommandHandlersTests.cs` | Create, SetPassword, Disable, Delete handlers + validators |
| `tests/Application.UnitTests/DirectoryQueryHandlersTests.cs` | Search*, Get*, parents handlers + validators |

## Files to Modify

(none; no production code changes)

## Tests

About 35 new tests across the two files. Full suite and a Release build must stay warning-free.

## Technical Notes

- Deviation from the work item note "use the mock adapter": the mock adapter is wired in Infrastructure (and the connector's runtime wiring is already exercised by the functional tests through the HTTP endpoints). A Moq connector tests the use-case boundary itself, keeps the tests in the Application test project and avoids depending on Infrastructure. Say so if you prefer endpoint tests through the mock adapter instead.
- The `Secret` domain primitive is not added (the work item says only "if cheap"; it has no tests today and is out of the use-case scope). Can be added on request.
- No Wolverine generated code changes are expected.

---
*Plan approved.*
