# Feature Specification: OAuth API Security

**Feature Branch**: `001-oauth-api-security`

**Created**: 2026-10-21

**Status**: Draft

**Input**: User description: "Add OAuth/OpenID security to Bitai.LDAPGateway.Api endpoints with configurable administrator and reader scopes, authority settings, and an API-wide unauthenticated bypass."

## Clarifications

### Session 2026-10-21

- Q: When the API-wide bypass is enabled, must the authority, API-resource, and scope settings still be valid at startup? → A: Only the bypass setting is required; authority, API-resource, and scope settings may be absent.
- Q: Which API endpoints should callers with only the reader scope be allowed to use? → A: All HTTP GET endpoints and authentication endpoints.
- Q: In which deployment environments may the API-wide unauthenticated bypass be enabled? → A: Any environment, including production, when explicitly enabled.
- Q: Which routes are the reader-accessible authentication endpoints? → A: `/api/{serverProfile}/{catalogType}/Authentications/authenticate` and `/api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Gateway Access (Priority: P1)

As a gateway operator, I want every API endpoint protected by verified caller credentials by default so that directory operations and data are not exposed to unauthenticated clients.

**Why this priority**: Default-secure endpoint access is the primary protection for all directory-facing capabilities.

**Independent Test**: Enumerate every mapped API endpoint and request it with missing, invalid, expired, wrong-authority, wrong-resource, insufficient-scope, and valid credentials.

**Acceptance Scenarios**:

1. **Given** the API security bypass is disabled, **When** an unauthenticated caller requests any API endpoint, **Then** the caller is denied without receiving directory data.
2. **Given** the API security bypass is disabled, **When** a caller presents invalid, expired, wrong-authority, or wrong-resource credentials, **Then** the caller is denied with a safe authorization failure.
3. **Given** a caller has valid credentials and the required access scope, **When** the caller requests an allowed endpoint, **Then** the gateway completes the request normally.

---

### User Story 2 - Apply Scope-Based Access (Priority: P1)

As an API operator, I want a configurable administrator scope to grant access to all endpoints and a configurable reader scope to grant reader-accessible endpoint access only so that callers receive the least access needed for their duties.

**Why this priority**: Administrator and reader access must remain distinct to protect directory-modifying operations.

**Independent Test**: Issue valid credentials containing administrator scope, reader scope, both scopes, and neither scope; exercise representative read and modifying endpoints.

**Acceptance Scenarios**:

1. **Given** valid credentials containing the configured administrator scope, **When** a caller requests any API endpoint, **Then** the request is authorized subject to existing validation and business rules.
2. **Given** valid credentials containing only the configured reader scope, **When** a caller requests an HTTP GET endpoint or one of the specified authentication endpoints, **Then** the request is authorized.
3. **Given** valid credentials containing only the configured reader scope, **When** a caller requests any other non-GET endpoint, **Then** the caller is denied and no directory data is modified.
4. **Given** valid credentials without either configured scope, **When** a caller requests an API endpoint, **Then** the caller is denied.

---

### User Story 3 - Operate an Explicit Security Bypass (Priority: P2)

As an operator, I want to explicitly enable an API security bypass in any deployment environment so that every endpoint accepts unauthenticated connections while retaining its existing input validation and business rules.

**Why this priority**: Operators may require temporary access when the identity authority is unavailable, but this must never weaken the secure default.

**Independent Test**: Enable the bypass, request every endpoint category without credentials, and verify the requests reach the endpoints' existing validation and business processing.

**Acceptance Scenarios**:

1. **Given** the API security bypass is explicitly enabled, **When** an unauthenticated caller requests any API endpoint, **Then** OAuth/OIDC credential and scope checks do not deny the request.
2. **Given** the API security bypass is explicitly enabled, **When** a caller requests an endpoint with invalid credentials, **Then** the credentials do not cause the request to be denied.
3. **Given** the API security bypass is explicitly enabled, **When** a caller submits invalid endpoint input, **Then** existing input validation and safe failure behavior remain in effect.

---

### User Story 4 - Configure and Detect Security Mode (Priority: P2)

As a gateway operator, I want to configure the trusted authority, intended API resource, metadata transport requirement, administrator scope, reader scope, and bypass state, and identify the active security mode through normal diagnostics.

**Why this priority**: Secure, predictable deployments require configuration validation and clear awareness when API-wide security is disabled.

**Independent Test**: Start the gateway with valid settings, then independently test missing, blank, malformed, and conflicting settings and inspect diagnostics for each valid security mode.

**Acceptance Scenarios**:

1. **Given** complete valid security settings, **When** the gateway starts, **Then** it applies them consistently to API access decisions.
2. **Given** bypass mode is disabled and security settings are missing, blank, malformed, or internally conflicting, **When** the gateway starts, **Then** it refuses to serve API requests.
3. **Given** bypass mode is enabled and the bypass setting is valid, **When** authority, API-resource, or scope settings are absent, **Then** the gateway starts in bypass mode.
4. **Given** the bypass is enabled or disabled, **When** the gateway starts, **Then** normal operational diagnostics identify the active security mode without exposing credentials or secrets.

### Edge Cases

- When the bypass configuration is absent, the gateway treats it as disabled.
- When the configured administrator and reader scope names are blank, identical, or otherwise conflicting, the gateway refuses to start.
- When a caller has both scopes, administrator access applies.
- When the bypass is enabled, the gateway bypasses OAuth/OIDC credential and scope checks only; it does not bypass endpoint validation, directory safety checks, or business rules.
- When bypass mode is disabled after use, administrator and reader scope rules apply to every endpoint again.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The gateway MUST protect every API endpoint with OAuth/OIDC credential validation and access-scope authorization when the bypass is disabled.
- **FR-002**: The gateway MUST accept protected requests only when caller credentials are valid, unexpired, issued by the configured authority, and intended for the configured API resource.
- **FR-003**: The gateway MUST authorize callers with the configured administrator scope to access every API endpoint.
- **FR-004**: The gateway MUST authorize callers with the configured reader scope to access every HTTP GET endpoint and these authentication endpoints: `/api/{serverProfile}/{catalogType}/Authentications/authenticate` and `/api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`. The gateway MUST deny reader-only callers access to every other non-GET endpoint unless they also have administrator access.
- **FR-005**: The gateway MUST provide configurable administrator-scope, reader-scope, trusted-authority, API-resource, metadata-transport, and bypass settings.
- **FR-006**: The bypass setting MUST default to disabled when absent.
- **FR-007**: When the bypass setting is explicitly enabled, the gateway MUST allow unauthenticated callers to reach every API endpoint and MUST not deny callers because of missing, invalid, expired, wrong-authority, wrong-resource, or insufficient-scope credentials.
- **FR-007a**: The gateway MUST permit the explicitly enabled bypass in any deployment environment, including production.
- **FR-008**: When the bypass setting is disabled, the gateway MUST validate the authority, API-resource, metadata-transport, and scope settings before serving requests and MUST refuse to start for missing, blank, malformed, or internally conflicting settings.
- **FR-008a**: When the bypass setting is enabled, the gateway MUST require only a valid bypass setting and MAY start without authority, API-resource, metadata-transport, or scope settings.
- **FR-009**: The gateway MUST expose the active bypass state through normal operational diagnostics without recording credentials, secrets, or authority internals.
- **FR-010**: The gateway MUST preserve endpoint input validation, directory safety checks, business rules, and safe error responses in both security modes.

### Key Entities

- **Caller Credential**: Evidence of caller identity, issuing authority, intended API resource, expiry, and granted access scopes.
- **Access Scope**: A configurable named permission; administrator scope grants every endpoint, while reader scope grants reader-accessible endpoints only.
- **Reader-Accessible Endpoint**: Any HTTP GET endpoint, plus `/api/{serverProfile}/{catalogType}/Authentications/authenticate` and `/api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`.
- **Security Configuration**: Operator-managed authority, API resource, metadata transport, scope-name, and bypass settings.
- **Security Mode**: Either standard protected mode or explicit API-wide bypass mode.
- **Endpoint Classification**: An endpoint's administrator-only, reader-accessible HTTP GET, or reader-accessible authentication category. The reader-accessible authentication endpoints are `/api/{serverProfile}/{catalogType}/Authentications/authenticate` and `/api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With bypass disabled, 100% of tested unauthenticated, invalid-credential, and insufficient-scope requests to every API endpoint are denied without returning directory data.
- **SC-002**: With bypass disabled, callers with the administrator scope successfully complete 100% of authorized requests to every endpoint, subject to existing input validation and business rules.
- **SC-003**: With bypass disabled, callers with only the reader scope successfully complete 100% of tested HTTP GET and authentication requests and are denied for 100% of tested non-GET, non-authentication requests.
- **SC-004**: With bypass enabled, 100% of tested unauthenticated requests to every API endpoint reach the endpoint's existing validation and business processing.
- **SC-005**: With bypass disabled, the gateway refuses to serve requests in 100% of tested cases with missing, blank, malformed, or conflicting required security configuration.
- **SC-005a**: With bypass enabled, the gateway starts in 100% of tested cases where only the valid bypass setting is provided.
- **SC-006**: Operators can determine the active security mode from normal diagnostics in under one minute without inspecting credentials or source code.

## Assumptions

- The configured OAuth/OIDC authority issues caller credentials and assigns the administrator and reader scopes.
- Reader-accessible endpoints are all HTTP GET endpoints plus `/api/{serverProfile}/{catalogType}/Authentications/authenticate` and `/api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`; every other non-GET endpoint requires administrator scope.
- Bypass mode may be enabled in any deployment environment, including production; it defaults to disabled and is an explicit operator decision.
- In bypass mode, authority, API-resource, metadata-transport, and scope settings may be omitted because OAuth/OIDC credential and scope checks are inactive.
- Existing endpoint contracts, validation behavior, and safe error-response conventions remain unchanged for callers that pass the active security mode.
- The gateway never logs or returns credentials, secrets, or sensitive authority details.
