# Code Review: swagger-ui-client-options

Files reviewed: `src/Api/Options/SwaggerUIApiClientOptions.cs`, `src/Api/Extensions/ServiceCollectionExtensions.cs`, `src/Api/appsettings.json`.

## Auto-fixes

None needed. Build is warning-free; style matches `AuthorityConfiguration` / `ApiScopesConfiguration` (4-space indent, Allman braces, `SectionName` constant, XML docs on public types).

## Checks

- Architecture: options and registration stay in Api; no layering impact.
- Security: no secret committed (`SwaggerUIClientSecret` is empty); validation messages do not echo values.
- Configuration: validated on start only when security is active, matching `AuthorityConfiguration`.

## Suggestions (not applied)

- Validate that the configured scope names are a subset of `AdminApiScopeName` / `ReaderApiScopeName`, to catch drift between Swagger and API scopes. Deferred; can be added in `swagger-ui-oauth-security-scheme`.
- Add boundary tests for the new validation (planned in `swagger-ui-oauth-tests`).
