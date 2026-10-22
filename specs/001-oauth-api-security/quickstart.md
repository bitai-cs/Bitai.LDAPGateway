# Quickstart: Validate OAuth API Security

## Prerequisites

1. A Duende IdentityServer environment that issues JWT access tokens for API resource `Bitai.LDAPGateway.Resource`.
2. Clients capable of obtaining tokens containing `Bitai.LDAPGateway.Scope.Admin` or `Bitai.LDAPGateway.Scope.Reader`.
3. The gateway configured with the authority and scope settings in [contracts/oauth-api-security.md](contracts/oauth-api-security.md).
4. A configured mock LDAP profile for functional tests.

## Standard security validation

1. Set `BypassApiScopesAuthorization` to `false`.
2. Start the API and confirm startup succeeds only with valid authority, resource, metadata, and distinct scope settings.
3. Send no bearer token to a reader endpoint; verify denial without directory data.
4. Send an invalid, expired, wrong-issuer, or wrong-audience token; verify denial without authority internals.
5. Send a reader-scoped token to:
   - an HTTP GET endpoint;
   - `POST /api/{serverProfile}/{catalogType}/Authentications/authenticate`; and
   - `POST /api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`.

   Verify each is accepted subject to existing endpoint validation.
6. Send the reader-scoped token to each modifying non-GET endpoint; verify denial and no directory modification.
7. Send an administrator-scoped token to representative read, authentication, and modifying endpoints; verify normal processing.

## Bypass validation

1. Set `BypassApiScopesAuthorization` to `true`; omit authority and scope configuration.
2. Start the API and confirm diagnostics identify bypass mode without secrets.
3. Send unauthenticated requests to representative GET, authentication, and modifying endpoints; verify they reach established validation/business behavior.
4. Submit invalid endpoint input in bypass mode; verify existing validation still returns the established safe error response.
5. Reset bypass to `false`; verify standard-mode startup validation and authorization rules apply again.

## Automated validation

Run the focused functional test project after implementation:

```powershell
dotnet test .\tests\Api.FunctionalTests\Bitai.LDAPGateway.Api.FunctionalTests.csproj
```

Then run the solution build to verify generated Wolverine handlers and warnings:

```powershell
dotnet build .\Bitai.LDAPGateway.slnx
```
