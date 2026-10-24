# Test Report: run-bitai-ldapgateway-008

- No behavior change, so no new tests.
- `dotnet build Bitai.LDAPGateway.slnx -c Release`: succeeded.
- Debug: Domain 1, Application 24, Functional 24 passed; external LDAPHelper tests 44 passed.
- Release: 7 `OAuthSecurityTests.StandardMode_*` tests fail with `ObjectDisposedException` (IServiceProvider). The same 7 fail on a clean checkout of HEAD, so they are pre-existing and unrelated to this run. Worth a separate work item.
