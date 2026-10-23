# Code Review: swagger-ui-oauth-tests

Files reviewed: `tests/Api.FunctionalTests/SwaggerUiOAuthTests.cs`.

## Auto-fixes

None needed. Build is warning-free; naming follows the existing tests.

## Checks

- Isolation: environment variables are set and restored in `finally`; the collection disables parallelization, so no leakage into other classes.
- Determinism: no network or live authority; the authority URL is a fixed test value and the secret is a dummy.
- Security: no real secret in the repository.

## Suggestions (not applied)

- Cover an empty scopes list by giving the API a way to override the list wholesale (not possible through environment variables today).
- Consider a shared test helper for env-var overrides if more suites need early configuration.
