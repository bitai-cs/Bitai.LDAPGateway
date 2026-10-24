---
id: mask-set-password-command-logging
title: Mask new password in SetMsAdUserPasswordCommand logging
intent: code-review-20251023-remediation
complexity: medium
mode: confirm
status: completed
depends_on: []
created: 2026-10-24T00:00:00Z
run_id: run-bitai-ldapgateway-005
completed_at: 2026-10-24T01:06:40.131Z
---

# Work Item: Mask new password in SetMsAdUserPasswordCommand logging

## Description

Make `SetMsAdUserPasswordCommand.PrintMembers` a `protected override` so Wolverine never logs the new password (review H1), and prove it with a bus-level test.

## Acceptance Criteria

- [ ] `PrintMembers` is `protected override bool` and prints `NewPassword = ***`.
- [ ] A test sends a valid command through the Wolverine bus with a failing handler and asserts the password never appears in captured logs.
- [ ] `dotnet test Bitai.LDAPGateway.slnx` passes.

## Technical Notes

Mirror `CreateMsAdUserCommand.PrintMembers`. The existing `MigrationBehaviorTests` only covers the validation-failure path; add a case where the handler itself fails.

## Dependencies

(none)
