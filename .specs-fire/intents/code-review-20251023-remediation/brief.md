---
id: code-review-20251023-remediation
title: Code Review 2025-10-23 Remediation
status: in_progress
created: 2026-10-24T00:00:00Z
---

# Intent: Code Review 2025-10-23 Remediation

## Goal

Apply the findings of `docs/code-review-20251023.md` that were verified against the code and are aligned with the project constitution, bringing the implementation into compliance with its own principles (II, V and Quality).

## Users

API maintainers, operators who deploy the gateway, and reviewers who rely on the constitution as the review baseline.

## Problem

The review found committed bind passwords, a secret that can reach Wolverine logs, ineffective validators, unguarded `.Entries` accesses, handlers that skip the shared domain-event behavior, silent dropping of unknown domain events, dead code, and missing boundary tests. Verification against the code confirmed these findings, with the adjustments noted below.

## Triage Result (verified against code and constitution)

| Finding | Decision |
|---|---|
| C1 committed passwords | Apply, adjusted: keep `appsettings.json` tracked (constitution requires static profiles there), blank the secrets, load them from env vars/user-secrets, validate at startup. Password rotation and git-history cleanup are manual operator tasks. |
| C2 external `Bitai.LDAPHelper` | Out of scope (infrastructure decision: submodule, NuGet or vendor). |
| H1 `PrintMembers` private | Apply. The method already masks the password but is never invoked because it is not `protected override`. |
| M1 handlers without `LdapHandlerBase` | Apply to all 4 handlers (GetProfileById, GetProfileIds, GetProfiles, GetCatalogTypes). Decision: inherit `LdapHandlerBase`. |
| M2 dead code | Apply. |
| M3 validators | Apply (`Secret` NotNull is a no-op; `CatalogType != LC` for set password). `Reason` validation excluded. |
| M4 null guards | Apply, broadened to every unguarded `.Entries` access in `BitaiLdapHelperProvider`. |
| M5 publisher drops unknown events | Apply. |
| M6 plaintext password in `LdapServerProfile` | Rejected: inherent to bind, DTO does not expose it. |
| Low items | Only XML docs on public types apply; brace style is already enforced by `.editorconfig`, `*.user` is not tracked. |
| Test gaps | Apply, limited to use cases without a boundary test. |

## Success Criteria

- No real credentials in any tracked file; service fails at startup when a profile's bind password is not supplied.
- `SetMsAdUserPasswordCommand` never exposes the new password in logs, proven by a bus-level test.
- Validators reject empty/whitespace passwords and password change against `CatalogType.LC`.
- No unguarded `.Entries` dereference in `BitaiLdapHelperProvider`.
- All use-case handlers publish `OperationCompletedDomainEvent`; unknown domain events are not silently dropped.
- No commented-out dead code in the named files; public types documented with XML comments.
- Every use case has at least one boundary test; `dotnet test Bitai.LDAPGateway.slnx` passes with a warning-free build.

## Constraints

- Follow `.specs-fire/standards/` and `docs/constitution.md`; warnings are errors.
- Do not change public API contracts beyond what the validation fixes imply.
- Keep `appsettings.Testing.json` working so tests run without a live directory.
