# Implementation Plan: swagger-ui-oauth-security-scheme

Mode: validate. Design: `.specs-fire/intents/swagger-ui-oauth-integration/work-items/swagger-ui-oauth-security-scheme-design.md` (checkpoint 1 approved).

## Approach

1. **OpenAPI scheme** (`ServiceCollectionExtensions.AddWebApiConfiguration`): switch to `AddOpenApiDocument((config, sp) => ...)`. Read `IOptions<ApiScopesConfiguration>`; when bypass is `false`, also read `AuthorityConfiguration` and `SwaggerUIApiClientOptions` and call `config.AddSecurity("oauth2", [], new OpenApiSecurityScheme { Type = OAuth2, Flow = AccessCode, AuthorizationUrl = {Authority}/connect/authorize, TokenUrl = {Authority}/connect/token, Scopes = name -> title })`. This also adds the global security requirement. When bypass is `true`, nothing is added.
2. **Swagger UI** (`ApplicationBuilderExtensions.UseSwaggerUiIfConfigured`): when bypass is `false`, pass a settings callback to `UseSwaggerUi` that sets `OAuth2Client` (`ClientId`, `ClientSecret`, PKCE enabled). When `true`, no callback.
3. **Scope alignment validation** (`AddApiSecurity`, security-active branch): add a validator on `SwaggerUIApiClientOptions` so every Swagger scope name must be one of `AdminApiScopeName` / `ReaderApiScopeName`; checked by `ValidateOnStart`.

## Files to Create

(none)

## Files to Modify

- `src/Api/Extensions/ServiceCollectionExtensions.cs`: scope-subset validation; OAuth2 scheme in `AddOpenApiDocument`
- `src/Api/Extensions/ApplicationBuilderExtensions.cs`: `OAuth2Client` settings in `UseSwaggerUi`

## Tests

Boundary tests are scoped to work item `swagger-ui-oauth-tests`. In this run: warning-free build, existing suite (the 5 pre-existing `OAuthSecurityTests` failures are known), and a manual check of `/swagger/v1/swagger.json` (or the configured document name) in both modes.

## Risks / Notes

- If NSwag 14.7.1 does not expose PKCE through `OAuth2ClientSettings`, fall back to Swagger UI `additionalQueryStringParams`/init settings and report it; if impossible, stop and ask before dropping PKCE.
- Redirect URI `{origin}/swagger/oauth2-redirect.html` must be registered at the authority; documented in the walkthrough.
