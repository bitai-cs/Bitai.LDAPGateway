---
id: remove-secrets-from-appsettings
title: Remove secrets from appsettings.json
intent: code-review-20251023-remediation
complexity: high
mode: validate
status: pending
depends_on: []
created: 2026-10-24T00:00:00Z
---

# Work Item: Remove secrets from appsettings.json

## Description

Remove the real bind passwords from the tracked `src/Api/appsettings.json` (review C1). Profiles stay as static entries in that file (constitution), but secrets are supplied at deploy time through environment variables or user secrets.

## Acceptance Criteria

- [ ] No real credential remains in any git-tracked file.
- [ ] `BindAccountPassword` in `appsettings.json` is empty/placeholder for every profile; env var / user-secrets override is documented.
- [ ] Startup validation fails when a profile that requires a bind password has none (outside Testing/mock profiles).
- [ ] `appsettings.Testing.json` and the functional tests still run without a live directory.
- [ ] Manual follow-ups (password rotation, history cleanup) are documented.
- [ ] `dotnet test Bitai.LDAPGateway.slnx` passes with a warning-free build.

## Technical Notes

Constitution Principle V and Configuration section. Rotation of the exposed passwords (including those in the untracked appsettings.Development.json) and git-history cleanup are manual operator tasks; document them. Needs a design doc (validate mode).

## Dependencies

(none)
