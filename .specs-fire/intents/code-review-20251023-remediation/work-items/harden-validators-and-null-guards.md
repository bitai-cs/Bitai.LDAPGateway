---
id: harden-validators-and-null-guards
title: Harden validators and null guards
intent: code-review-20251023-remediation
complexity: medium
mode: confirm
status: completed
depends_on: []
created: 2026-10-24T00:00:00Z
run_id: run-bitai-ldapgateway-006
completed_at: 2026-10-24T01:22:25.679Z
---

# Work Item: Harden validators and null guards

## Description

Fix ineffective validation (review M3) and null dereferences on `searchResult.Entries` (review M4).

## Acceptance Criteria

- [ ] Authenticate and AuthenticateWithoutUserLookup validators reject empty/whitespace passwords.
- [ ] `SetMsAdUserPasswordCommandValidator` rejects `CatalogType.LC`.
- [ ] No unguarded `.Entries` access remains in `BitaiLdapHelperProvider`.
- [ ] Validator tests cover the new rules.
- [ ] `dotnet test Bitai.LDAPGateway.slnx` passes.

## Technical Notes

`Secret` is a struct so `NotNull()` is a no-op: validate the underlying value is not empty/whitespace. Guard every unguarded `.Entries` access in `BitaiLdapHelperProvider` (GetUserAsync, GetUserParentsAsync, GetGroupAsync, GetGroupParentsAsync) with `?? Array.Empty<LDAPEntry>()`. `DisableMsAdUserCommand.Reason` validation is out of scope.

## Dependencies

(none)
