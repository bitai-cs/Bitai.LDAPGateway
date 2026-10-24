# Test Report: run-bitai-ldapgateway-009

New tests in `tests/Api.FunctionalTests/LdapServerProfilesValidatorTests.cs` (6):
- Novell + blank password fails and names the profile.
- Novell + password passes.
- Mock + blank password passes.
- One blank profile among several: only that profile is named; the password value never appears in the message.
- Host with Novell adapter and passwords supplied via configuration starts (control).
- Host with Novell adapter and blank passwords fails to start.

Full suite (Debug): Domain 1, Application 24, Functional 30, external LDAPHelper 44; all pass. Release build succeeds with no warnings from this repo.

Note: the host-failure test asserts only that startup throws. The underlying exception surfaces as `ObjectDisposedException` (the same masking seen in the pre-existing Release OAuth failures), so the message is asserted at validator level instead. The passing control test ties the failure to the missing passwords.
