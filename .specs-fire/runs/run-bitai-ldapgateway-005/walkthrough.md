# Walkthrough: Mask new password in SetMsAdUserPasswordCommand logging

Run: run-bitai-ldapgateway-005 | Intent: code-review-20251023-remediation

## Outcome

Review finding **H1 is a false positive**. No production code was changed.

## Why

`SetMsAdUserPasswordCommand` is a `sealed record` deriving from `object`. For that shape the compiler recognizes `private bool PrintMembers(StringBuilder)` and uses it in the synthesized `ToString()`. `protected override` (suggested by the review) would not compile here; `CreateMsAdUserCommand` uses it only because it derives from the record `CreateMsAdUserDto`. The method already prints `NewPassword = ***`.

## What was added

- `tests/Application.UnitTests/SetMsAdUserPasswordCommandTests.cs`: asserts `ToString()` never contains the password and contains `NewPassword = ***`. It passes on the unmodified code and guards against regressions.

## What was not done

A bus-level test with a failing handler was attempted and dropped: Wolverine's static pre-generated handlers instantiate the real connector, so a DI-substituted connector is ignored and the handler-failure path cannot be forced that way.

## Verify

`dotnet test Bitai.LDAPGateway.slnx` (all pass; only the 3 known NU190x warnings from the external `Bitai.LDAPWebApi`).
