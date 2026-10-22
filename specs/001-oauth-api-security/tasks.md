---

description: "Executable task list for OAuth API Security"
---

# Tasks: OAuth API Security

**Input**: Design documents from `specs/001-oauth-api-security/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/oauth-api-security.md`, `quickstart.md`

**Tests**: Functional tests are required by the specification acceptance scenarios and the constitution's boundary-test requirement. They must use a local test authentication setup rather than a live Duende IdentityServer.

**Organization**: Tasks are grouped by user story so each increment can be implemented and verified independently after foundational security registration is available.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel because it changes a different file and has no dependency on incomplete work.
- **[Story]**: Maps the task to a user story in `spec.md`.

## Phase 1: Setup

**Purpose**: Add the authentication dependency and secure-mode configuration defaults.

- [ ] T001 Add the .NET 10-compatible `Microsoft.AspNetCore.Authentication.JwtBearer` package reference in `src/Api/Bitai.LDAPGateway.Api.csproj`
- [ ] T002 [P] Add the default-disabled `ApiScopesConfiguration` and `AuthorityConfiguration` examples in `src/Api/appsettings.json`
- [ ] T003 [P] Add the default-disabled `ApiScopesConfiguration` and local Duende authority examples in `src/Api/appsettings.Development.json`

---

## Phase 2: Foundational Security Infrastructure

**Purpose**: Define validated security configuration and common policy registration before endpoint-specific access rules are applied.

**CRITICAL**: Complete this phase before implementing any user story.

- [ ] T004 [P] Create `AuthorityConfiguration` options with `Authority`, `ApiResource`, and `RequireHttpsMetadata` properties in `src/Api/Options/AuthorityConfiguration.cs`
- [ ] T005 [P] Create `ApiScopesConfiguration` options with `BypassApiScopesAuthorization`, `AdminApiScopeName`, and `ReaderApiScopeName` properties in `src/Api/Options/ApiScopesConfiguration.cs`
- [ ] T006 Create named authorization-policy constants for administrator and reader-or-administrator access in `src/Api/Authorization/ApiAuthorizationPolicies.cs`
- [ ] T007 Extend `src/Api/Extensions/ServiceCollectionExtensions.cs` with conditional options validation: when bypass is disabled require non-blank authority/resource/scope values and distinct administrator/reader scopes; when bypass is enabled require only a valid bypass setting
- [ ] T008 Extend `src/Api/Extensions/ServiceCollectionExtensions.cs` with standard-mode JWT bearer registration using the Duende authority, configured API resource audience, HTTPS metadata setting, administrator fallback policy, and reader-or-administrator policy
- [ ] T009 Update middleware order in `src/Api/Program.cs` to invoke authentication before authorization and register the security services from `ServiceCollectionExtensions`

**Checkpoint**: The API has configuration validation, a Duende-compatible bearer-validation path, and named policy infrastructure.

---

## Phase 3: User Story 1 - Secure Gateway Access (Priority: P1) 🎯 MVP

**Goal**: Deny missing, invalid, expired, wrong-authority, and wrong-resource credentials for every API endpoint while allowing valid administrator credentials.

**Independent Test**: With bypass disabled, issue unauthenticated/invalid-token requests and an administrator-scoped test identity request to representative read and modifying routes; only the valid administrator request reaches established endpoint behavior.

- [ ] T010 [P] [US1] Create a controllable test-authentication handler and test web-application-factory configuration in `tests/Api.FunctionalTests/OAuthSecurityWebApplicationFactory.cs`
- [ ] T011 [US1] Add functional cases for absent credentials, invalid credentials, expired credentials, wrong authority, wrong resource, and valid administrator credentials in `tests/Api.FunctionalTests/OAuthSecurityTests.cs`
- [ ] T012 [US1] Apply administrator fallback authorization to all API controller endpoints through `src/Api/Extensions/ServiceCollectionExtensions.cs` and verify no controller action remains unintentionally unprotected
- [ ] T013 [US1] Update existing anonymous-route assertions to use an administrator-scoped test identity in `tests/Api.FunctionalTests/ApiRoutesTests.cs`

**Checkpoint**: Standard mode denies invalid callers across the gateway and still permits authorized administrator callers.

---

## Phase 4: User Story 2 - Apply Scope-Based Access (Priority: P1)

**Goal**: Give reader-scoped callers access to every HTTP GET action and exactly the two approved authentication POST actions, while keeping all other non-GET actions administrator-only.

**Independent Test**: With bypass disabled, execute a GET action and each approved authentication route with reader scope, then execute a modifying non-GET action with the same identity and verify denial without a directory change.

- [ ] T014 [P] [US2] Add reader-or-administrator authorization metadata to all HTTP GET actions in `src/Api/Controllers/CatalogTypesController.cs`, `src/Api/Controllers/ServerProfilesController.cs`, and `src/Api/Controllers/DirectoryController.cs`
- [ ] T015 [P] [US2] Add reader-or-administrator authorization metadata to only `authenticate` and `authenticateWithoutUserLookup` in `src/Api/Controllers/AuthenticationsController.cs`
- [ ] T016 [US2] Add reader-only functional cases for HTTP GET actions, both authentication routes, a modifying non-GET denial, and no-scope denial in `tests/Api.FunctionalTests/OAuthSecurityTests.cs`

**Checkpoint**: Reader permissions are explicit, limited to the agreed endpoint set, and independently validated.

---

## Phase 5: User Story 3 - Operate an Explicit Security Bypass (Priority: P2)

**Goal**: When explicitly enabled, permit unauthenticated access to every endpoint without requiring authority, resource, metadata, or scope configuration, while preserving endpoint validation and business rules.

**Independent Test**: Start a bypass-configured test host with only a valid bypass setting, call representative GET, authentication, and modifying routes without credentials, and confirm invalid payloads still use established validation failures.

- [ ] T017 [US3] Extend `src/Api/Extensions/ServiceCollectionExtensions.cs` with bypass-mode permissive authorization policies that allow anonymous callers without registering JWT bearer validation or requiring authority/scope configuration
- [ ] T018 [US3] Add bypass-mode functional startup, unauthenticated GET/authentication/modifying route, invalid-credential, and invalid-input cases in `tests/Api.FunctionalTests/OAuthSecurityTests.cs`

**Checkpoint**: Bypass mode is isolated to an explicit startup configuration and does not alter non-security endpoint behavior.

---

## Phase 6: User Story 4 - Configure and Detect Security Mode (Priority: P2)

**Goal**: Validate configuration according to the active security mode and make that mode observable without exposing sensitive values.

**Independent Test**: Verify standard-mode startup rejects missing, blank, malformed, and conflicting authority/scope settings; verify bypass-mode startup accepts only the bypass setting; inspect captured diagnostics for each mode.

- [ ] T019 [US4] Add safe standard-mode and bypass-mode startup diagnostics, including an explicit warning for API-wide bypass activation, in `src/Api/Program.cs`
- [ ] T020 [US4] Add functional configuration-validation and non-secret security-mode diagnostic assertions in `tests/Api.FunctionalTests/OAuthSecurityConfigurationTests.cs`

**Checkpoint**: Operators can identify the active security mode and invalid standard-mode configuration blocks startup.

---

## Phase 7: Polish and Cross-Cutting Concerns

**Purpose**: Publish the security contract, verify generated artifacts, and validate the completed feature against its quickstart.

- [ ] T021 [P] Add bearer-security definitions and administrator versus reader operation requirements to the OpenAPI configuration in `src/Api/Extensions/ServiceCollectionExtensions.cs`
- [ ] T022 Regenerate and commit affected Wolverine generated handlers under `src/Api/Internal/Generated/` using the repository's code-generation workflow
- [ ] T023 Run the end-to-end scenarios in `specs/001-oauth-api-security/quickstart.md`, then run `dotnet test .\tests\Api.FunctionalTests\Bitai.LDAPGateway.Api.FunctionalTests.csproj` and `dotnet build .\Bitai.LDAPGateway.slnx`

---

## Dependencies and Execution Order

### Phase Dependencies

1. Phase 1 has no dependencies.
2. Phase 2 depends on Phase 1 and blocks all user stories.
3. US1 depends on Phase 2.
4. US2 depends on US1's working standard-mode policy registration.
5. US3 depends on Phase 2 but can proceed in parallel with US2 after standard-mode test infrastructure is available.
6. US4 depends on Phase 2 and can proceed in parallel with US2/US3 after option types and registration exist.
7. Polish depends on all selected user stories.

### User Story Dependencies

- **US1 — Secure Gateway Access**: MVP; establish standard-mode protection first.
- **US2 — Scope-Based Access**: Requires US1's standard-mode policies and test host.
- **US3 — Security Bypass**: Requires foundational options and policy registration; does not depend on reader endpoint metadata.
- **US4 — Configure and Detect Security Mode**: Requires foundational options; does not depend on endpoint classification.

### Parallel Opportunities

- T002 and T003 can run in parallel.
- T004 and T005 can run in parallel.
- T010 can begin after Phase 2 while controller-policy work proceeds.
- T014 and T015 can run in parallel.
- After Phase 2, US3 and US4 can be assigned independently from US2.
- T021 can run after endpoint policy mapping is complete while functional validation proceeds.

## Parallel Example: User Story 2

```text
Task: "Add reader-or-administrator authorization metadata to all HTTP GET actions in src/Api/Controllers/CatalogTypesController.cs, src/Api/Controllers/ServerProfilesController.cs, and src/Api/Controllers/DirectoryController.cs"
Task: "Add reader-or-administrator authorization metadata to only authenticate and authenticateWithoutUserLookup in src/Api/Controllers/AuthenticationsController.cs"
```

## Implementation Strategy

### MVP First

1. Complete Phases 1 and 2.
2. Complete US1 and validate standard-mode authentication with administrator access.
3. Stop and validate that every existing endpoint is protected before adding reader and bypass behaviors.

### Incremental Delivery

1. Deliver US1: secure default access with administrator scope.
2. Deliver US2: reader scope access to agreed read and authentication routes.
3. Deliver US3: explicit, default-disabled API-wide bypass.
4. Deliver US4: mode-aware startup validation and operator diagnostics.
5. Complete OpenAPI, code generation, build, and quickstart validation.
