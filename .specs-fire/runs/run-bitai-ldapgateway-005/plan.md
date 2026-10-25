---
run: run-bitai-ldapgateway-005
work_item: mask-set-password-command-logging
intent: code-review-20251023-remediation
mode: confirm
checkpoint: plan
approved_at: 2026-10-24T01:00:00Z
---

# Implementation Plan: Mask new password in SetMsAdUserPasswordCommand logging

## Approach

`SetMsAdUserPasswordCommand` is a positional `record`, so the compiler-synthesized `ToString()` calls a `PrintMembers(StringBuilder)` method only if it has the exact shape the compiler expects. Because the sealed record declares it `private`, the synthesized one is the one that is used and `NewPassword` is printed in clear text. Per the C# record rules, a `sealed` record must declare it as `private bool PrintMembers(StringBuilder)`, and a non-sealed record as `protected virtual`. This needs verification before changing the modifier (see Technical Details).

1. Reproduce the leak with a failing bus-level test (handler throws) before touching the command.
2. Fix `PrintMembers` so the record's `ToString()` prints `NewPassword = ***`.
3. Confirm the test passes and the existing tests still pass.

## Files to Create

(none)

## Files to Modify

| File | Changes |
|------|---------|
| `src/Application/Directory/Commands/SetMsAdUserPassword/SetMsAdUserPasswordCommand.cs` | Correct `PrintMembers` so it is the one used by the synthesized `ToString()` and prints `NewPassword = ***` |
| `tests/Api.FunctionalTests/MigrationBehaviorTests.cs` | Add `FailedSetPasswordHandler_DoesNotWritePasswordToLogs`: a valid command whose handler fails (connector replaced by a throwing stub) must not leak the password in captured logs |

## Tests

| Test File | Coverage |
|-----------|----------|
| `tests/Api.FunctionalTests/MigrationBehaviorTests.cs` | New handler-failure leak test, run against the unfixed code first to prove it fails, then against the fix |

## Technical Details

- The review says to use `protected override`, but that only compiles for a non-sealed record whose base type declares a virtual `PrintMembers`. `SetMsAdUserPasswordCommand` is `sealed` and derives from `object`, so `protected override` will not compile. `CreateMsAdUserCommand` can use it because it derives from `CreateMsAdUserDto`, a record. I will check the real compiler behavior first and report the exact outcome, rather than assume.
- The leak happens in the handler-failure path (an exception thrown while handling), not in validation, which is why the existing test does not catch it.
- Test scaffolding: `OAuthSecurityWebApplicationFactory` with `WithWebHostBuilder` replacing `IDirectoryServiceConnector` with a Moq stub that throws, plus the existing `CapturedLogs` provider and `Leaks` helper.
- No production behavior changes beyond the log text.

---
*Plan approved.*
