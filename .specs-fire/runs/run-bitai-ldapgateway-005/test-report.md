---
run: run-bitai-ldapgateway-005
work_item: mask-set-password-command-logging
intent: code-review-20251023-remediation
---

# Test Report: Mask new password in SetMsAdUserPasswordCommand logging

## Test Results

`dotnet test Bitai.LDAPGateway.slnx`

| Project | Passed | Failed |
|---|---|---|
| Domain.UnitTests | 1 | 0 |
| Application.UnitTests | 7 (1 new) | 0 |
| Api.FunctionalTests | 23 | 0 |
| Bitai.LDAPHelper.Tests (external) | 44 | 0 |

New test: `SetMsAdUserPasswordCommandTests.ToString_MasksNewPassword`.

## Acceptance Criteria Validation

- [~] `PrintMembers` is `protected override`: **not applicable.** The record is `sealed` and derives from `object`, so the correct (and compiler-recognized) form is `private bool PrintMembers(StringBuilder)`, which is what it already is. Review finding H1 is a false positive; no production change was made.
- [x] `PrintMembers` prints `NewPassword = ***`: verified by the new unit test against the unmodified code.
- [~] Bus-level leak test with a failing handler: attempted and removed. Wolverine uses statically pre-generated handler code that instantiates the real connector, so a DI-substituted throwing connector is ignored and the failure path cannot be forced through the bus. The root cause (the record's `ToString()`, which Wolverine uses for logging) is covered directly by the unit test. The existing functional test `FailedSetPasswordCommand_DoesNotWritePasswordToLogs` still covers the validation-failure path.
- [x] `dotnet test Bitai.LDAPGateway.slnx` passes.

## Notes

Build warnings: only the 3 known NU1902/NU1903 warnings from the external `Bitai.LDAPWebApi` project, which is not part of this repository.
