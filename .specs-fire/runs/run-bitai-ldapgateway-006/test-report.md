---
run: run-bitai-ldapgateway-006
work_item: harden-validators-and-null-guards
intent: code-review-20251023-remediation
---

# Test Report: Harden validators and null guards

`dotnet test Bitai.LDAPGateway.slnx`: all passing.

| Project | Passed | Failed |
|---|---|---|
| Domain.UnitTests | 1 | 0 |
| Application.UnitTests | 19 (12 new) | 0 |
| Api.FunctionalTests | 23 | 0 |
| Bitai.LDAPHelper.Tests (external) | 44 | 0 |

## Acceptance Criteria Validation

- [x] Authenticate and AuthenticateWithoutUserLookup validators reject null, empty, whitespace and `default(Secret)` passwords (new theories/facts), and accept a valid one.
- [~] `SetMsAdUserPasswordCommandValidator` rejects `CatalogType.LC`: **intentionally not done.** The review is internally inconsistent (says global catalog, proposes excluding LC) and the code does not show which catalog rejects a password change. Decision recorded with the user; no rule added.
- [x] No unguarded `.Entries` access remains in `BitaiLdapHelperProvider` (10 sites in 4 methods guarded).
- [x] `dotnet test Bitai.LDAPGateway.slnx` passes. Only the 3 known NU190x warnings from the external `Bitai.LDAPWebApi` project.

## Notes

No dedicated test for the null guards: `LDAPSearchResult.Entries` cannot be null via the library's own constructor and the provider cannot be exercised without the real library.
