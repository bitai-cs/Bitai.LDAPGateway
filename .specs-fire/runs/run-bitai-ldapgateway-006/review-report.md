# Code Review: run-bitai-ldapgateway-006

- Validators: `OverridePropertyName("Password")` keeps the API error property name stable and hides the accessor expression. `DangerousGetSecret()` is used only inside validation, never logged.
- Null guards follow the idiom already present in the file; redundant parentheses around the argument of `SearchParentEntriesAsync` were removed.
- Tests follow the existing xUnit conventions.
- Process note: the null-guard edit was applied before the plan checkpoint; it was disclosed and then approved with the plan.
- No issues found; no auto-fixes applied.
