# Walkthrough: Harden validators and null guards

Run: run-bitai-ldapgateway-006 | Intent: code-review-20251023-remediation

## Changes

- `AuthenticateCommand.cs`, `AuthenticateWithoutUserLookupCommand.cs`: `NotNull()` on a `Secret` struct could never fail; the validators now require the wrapped value to be non-empty (`NotEmpty()` also rejects whitespace and `default(Secret)`).
- `BitaiLdapHelperProvider.cs`: `.Entries` is null-guarded with `?? Array.Empty<LDAPEntry>()` in GetUserAsync, GetUserParentsAsync, GetGroupAsync and GetGroupParentsAsync, matching the other methods.
- `tests/Application.UnitTests/AuthenticationValidatorsTests.cs`: 12 cases.

## Not done, needs your decision

`SetMsAdUserPasswordCommandValidator` has no `CatalogType` rule. The review text says "global catalog" but recommends `NotEqual(LC)`. Separately, `CreateMsAdUserCommandValidator` rejects `LC` with the message "Cannot create user accounts in the global catalog", which only makes sense if `LC` is the global catalog or the message is wrong. Confirm what `LC` and `GC` mean in your LDAPHelper profiles and fix the message or the rule accordingly.

## Verify

`dotnet test Bitai.LDAPGateway.slnx`.
