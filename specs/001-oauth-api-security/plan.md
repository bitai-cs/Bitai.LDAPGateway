# Implementation Plan: OAuth API Security

**Branch**: `001-oauth-api-security` | **Date**: 2026-10-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-oauth-api-security/spec.md`

## Summary

Secure every `Bitai.LDAPGateway.Api` endpoint with Duende IdentityServer-issued OAuth/OIDC bearer tokens when `BypassApiScopesAuthorization` is disabled. Validate token issuer and audience using `AuthorityConfiguration`; grant administrator-scope callers access to all endpoints, and reader-scope callers access to all HTTP GET endpoints plus the two authentication endpoints. When the explicitly configured bypass is enabled, allow unauthenticated access to every API endpoint and permit startup without authority or scope settings while retaining endpoint validation and business rules.

## Technical Context

**Language/Version**: C# / .NET 10, nullable enabled

**Primary Dependencies**: ASP.NET Core MVC, WolverineFx 6.44, NSwag 14.7, FluentValidation through Wolverine; add the .NET 10-compatible `Microsoft.AspNetCore.Authentication.JwtBearer` package. Duende IdentityServer is an external OAuth/OIDC authority reference, not a dependency hosted by this gateway.

**Storage**: N/A; the gateway remains stateless and uses configured LDAP profiles only.

**Testing**: xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing` 10.0, functional tests with a mock LDAP adapter; use a local OIDC discovery/JWKS fixture and signed JWTs to prove JwtBearer issuer, audience, expiry, and scope validation without a live Duende instance. A controllable test-authentication handler may be retained only for focused authorization-policy routing tests.

**Target Platform**: Cross-platform ASP.NET Core web service.

**Project Type**: REST web service using Clean Architecture (`Domain <- Application <- Infrastructure <- Api`).

**Performance Goals**: Preserve existing endpoint responsiveness; authorization adds only local token validation after cached authority metadata and signing keys are established.

**Constraints**: All standard-mode endpoints require valid issuer and audience before handler execution; default bypass is false; bypass may run in any environment, including production, and must be prominently logged without secrets. In bypass mode, only the bypass option is required at startup.

**Scale/Scope**: Secure all current API controllers and any subsequently mapped controller endpoints. Reader access is all HTTP GET actions plus the two specified `Authentications` POST actions; all other non-GET actions require administrator access.

## Constitution Check

*GATE: Passed before Phase 0 research. Re-checked and passed after Phase 1 design.*

| Principle | Plan response | Status |
|-----------|---------------|--------|
| Stateless, Directory-Neutral Gateway | Adds no persisted identity data or directory-product behavior; token validation belongs solely to the API boundary. | Pass |
| Clean Architecture and Explicit Use Cases | Authentication and authorization remain in `Api`; Application, Domain, and Infrastructure retain no HTTP or authority dependency. | Pass |
| Intentional Domain Modelling | Uses small API configuration and authorization-policy types only; no aggregates, repositories, or persistence are added. | Pass |
| Stable, Safe API Contracts | Preserves existing endpoint outputs and RFC 7807 error behavior; documents new 401/403 access decisions and OpenAPI security metadata. | Pass |
| Security by Design | Validates issuer, audience, expiry, and scope in standard mode; defaults bypass off; prevents secret logging. The production-capable bypass is an explicit documented trust decision. | Pass with documented risk |
| Configuration and Operations | Uses validated startup options. Standard mode rejects missing or conflicting authority/scope configuration; bypass mode intentionally permits those settings to be absent. | Pass |
| Quality and Evolution | Adds boundary-focused functional tests using the mock adapter, retains warnings-as-errors expectations, and regenerates/commits Wolverine code if changed. | Pass |

## Project Structure

### Documentation (this feature)

```text
specs/001-oauth-api-security/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
```text
src/
├── Api/
│   ├── Controllers/
│   │   ├── AuthenticationsController.cs
│   │   ├── CatalogTypesController.cs
│   │   ├── DirectoryController.cs
│   │   └── ServerProfilesController.cs
│   ├── Extensions/
│   ├── Options/
│   ├── Program.cs
│   └── appsettings*.json
├── Application/
├── Domain/
└── Infrastructure/
tests/
└── Api.FunctionalTests/
    └── ApiRoutesTests.cs
```

**Structure Decision**: Implement all security integration in the API host. Controller policy metadata, options, service registration, middleware order, API documentation, and functional tests are the only affected surfaces.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Production-capable API-wide bypass | The approved specification requires explicit unauthenticated access in any environment, including production. | Restricting bypass to non-production conflicts with FR-007a. The implementation mitigates risk through default-off configuration, startup validation of the bypass value, and prominent non-secret diagnostics. |
