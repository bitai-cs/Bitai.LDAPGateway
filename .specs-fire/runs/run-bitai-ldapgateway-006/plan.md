---
run: run-bitai-ldapgateway-006
work_item: harden-validators-and-null-guards
intent: code-review-20251023-remediation
mode: confirm
checkpoint: plan
approved_at: 2026-10-24T01:20:00Z
---

# Implementation Plan: Harden validators and null guards

## Approach

1. **Password validators (M3, verified):** `Secret` is a struct, so `NotNull()` on it can never fail. Validate the wrapped value instead (`Password.DangerousGetSecret()` not empty/whitespace) in `AuthenticateCommandValidator` and `AuthenticateWithoutUserLookupCommandValidator`, with the property name overridden to `Password` so the message does not expose the accessor name.
2. **Null guards (M4):** `LDAPSearchResult.Entries` is a non-nullable settable property initialized to an empty list by its constructor, so the risk is low, but 4 methods dereference it without the `?? Array.Empty<LDAPEntry>()` guard used elsewhere in the file. Apply the same idiom. *Already applied before this checkpoint (process slip); revert on request.*
3. **Catalog rule for SetMsAdUserPassword (M3, dropped by decision):** the review's text (global catalog) contradicts its own recommendation (`NotEqual(LC)`), and the code does not show which catalog rejects a password change. No rule is added. `CreateMsAdUserCommandValidator` already rejects `LC` with a message that says "global catalog"; that ambiguity is recorded in the walkthrough for you to resolve.
4. Tests for the new validator behavior.

## Files to Create

| File | Purpose |
|------|---------|
| `tests/Application.UnitTests/AuthenticationValidatorsTests.cs` | Both authentication validators reject default/empty/whitespace passwords and accept a valid one |

## Files to Modify

| File | Changes |
|------|---------|
| `src/Application/Authentications/Commands/Authenticate/AuthenticateCommand.cs` | Replace ineffective `NotNull()` with a non-empty check on the secret value |
| `src/Application/Authentications/Commands/AuthenticateWithoutUserLookup/AuthenticateWithoutUserLookupCommand.cs` | Same |
| `src/Infrastructure/Services/BitaiLdapHelperProvider.cs` | Null-guard `.Entries` in GetUserAsync, GetUserParentsAsync, GetGroupAsync, GetGroupParentsAsync (done) |

## Tests

| Test File | Coverage |
|-----------|----------|
| `tests/Application.UnitTests/AuthenticationValidatorsTests.cs` | Empty, whitespace and `default(Secret)` passwords rejected; valid password accepted |

## Technical Details

- A `default(Secret)` has a null inner value; `NotEmpty()` on the string rejects it, which is the case the old rule missed.
- No test for the null guards: `Entries` cannot be null through the library's own constructor and the provider is not unit-testable without the live library; the change is verified by compilation and the existing functional suite.
- Out of scope per the intent: `DisableMsAdUserCommand.Reason` validation.

---
*Plan approved.*
