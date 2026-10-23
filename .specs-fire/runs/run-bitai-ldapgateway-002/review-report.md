# Code Review: swagger-ui-oauth-security-scheme

Files reviewed: `src/Api/Extensions/ServiceCollectionExtensions.cs`, `src/Api/Extensions/ApplicationBuilderExtensions.cs`.

## Auto-fixes

None needed. Build is warning-free and style matches the surrounding code.

## Checks

- Security: the secret is only read from configuration and passed to Swagger UI settings; never logged. Validation messages do not echo values.
- Behavior: bypass mode adds nothing to the document or UI; the options are not registered there, and they are only resolved when bypass is false.
- Architecture: changes stay in the Api layer.

## Suggestions (not applied)

- Authorize/token URLs follow the `/connect/*` convention; add optional override keys if a non-IdentityServer authority is used.
- `SwaggerOAuth2SchemeName` is a public const on the extensions class; could be internal if not reused by tests.
