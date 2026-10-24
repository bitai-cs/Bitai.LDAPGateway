---
id: use-case-boundary-tests
title: Boundary tests for untested use cases
intent: code-review-20251023-remediation
complexity: medium
mode: confirm
status: pending
depends_on: [mask-set-password-command-logging, harden-validators-and-null-guards, uniform-domain-events-for-profile-handlers]
created: 2026-10-24T00:00:00Z
---

# Work Item: Boundary tests for untested use cases

## Description

Add at least one boundary test (handler or endpoint) for every use case that lacks one, per testing standards.

## Acceptance Criteria

- [ ] Every use case has at least one boundary test.
- [ ] Tests run without a live directory.
- [ ] `dotnet test Bitai.LDAPGateway.slnx` passes with a warning-free build.

## Technical Notes

Use the mock adapter. Candidates: CreateMsAdUser, SetMsAdUserPassword, DisableMsAdUser, DeleteMsAdUser, SearchDirectory/Users/Groups, GetUser/Group/DirectoryEntry lookups and parents, profile handlers. Also cover Domain primitives (Secret) if cheap.

## Dependencies

- mask-set-password-command-logging
- harden-validators-and-null-guards
- uniform-domain-events-for-profile-handlers
