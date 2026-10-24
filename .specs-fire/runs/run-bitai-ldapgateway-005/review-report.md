# Code Review: run-bitai-ldapgateway-005

Scope: one new test file (`tests/Application.UnitTests/SetMsAdUserPasswordCommandTests.cs`); no production code changed.

- Follows the existing xUnit conventions and namespace of `Application.UnitTests`.
- Asserts both the absence of the secret and the presence of the `***` mask, so it fails if the method is renamed or stops being used.
- No issues found; no auto-fixes applied.
