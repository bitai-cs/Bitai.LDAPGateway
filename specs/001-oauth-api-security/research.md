# Research: OAuth API Security

## Duende IdentityServer authority model

### Decision

Use Duende IdentityServer as the reference authority. Configure it with the API resource `Bitai.LDAPGateway.Resource` and the API scopes `Bitai.LDAPGateway.Scope.Admin` and `Bitai.LDAPGateway.Scope.Reader`. Configure the gateway to validate bearer tokens from `AuthorityConfiguration:Authority`, require the configured API resource as the audience, and read granted API scopes from the access token.

### Rationale

Duende API resources represent protected APIs and produce the token audience claim; API scopes represent caller permissions. This maps directly to the gateway's configured resource and the administrator/reader access model. Authority discovery provides signing-key rotation without embedding keys in the gateway.

### Alternatives considered

- Self-issued tokens or embedded signing keys: rejected because they duplicate authority responsibility in the gateway.
- Token introspection as the primary validation path: rejected because the specified authority/resource configuration and stateless gateway fit JWT bearer validation; introspection can be considered later if opaque tokens become a requirement.

## Standard security mode

### Decision

Use the ASP.NET Core JWT bearer authentication handler in standard mode. Set its authority, HTTPS metadata requirement, and audience from validated configuration. Add named authorization policies for administrator and reader access that evaluate Duende-issued access-token scopes.

### Rationale

The standard handler rejects malformed, expired, wrong-issuer, and wrong-audience tokens before controller actions run. Named policies make endpoint trust decisions explicit and independently testable.

### Alternatives considered

- Manual token parsing in controllers: rejected because it risks inconsistent validation and violates the API-boundary policy pattern.
- One global administrator-only policy: rejected because the reader scope must access defined read and authentication endpoints.

## Bypass mode

### Decision

At startup, choose a permissive default authorization policy only when `BypassApiScopesAuthorization` is explicitly true. In that mode, do not register or require authority, audience, or scope configuration. All `[Authorize]` endpoint metadata therefore succeeds without an authenticated identity.

### Rationale

The specification requires the bypass to accept unauthenticated callers across every API endpoint and to start with only a valid bypass setting. Startup-selected policy registration makes this condition deterministic and avoids per-controller bypass logic.

### Alternatives considered

- `[AllowAnonymous]` attributes: rejected because attributes cannot vary safely by configuration without weakening standard mode.
- A custom authorization handler that still requires an authenticated user: rejected because it would not allow missing credentials.

## Endpoint policy mapping

### Decision

Apply the administrator policy by default to every controller/action. Apply the reader-or-administrator policy to all HTTP GET actions and the two authentication actions:

- `POST /api/{serverProfile}/{catalogType}/Authentications/authenticate`
- `POST /api/{serverProfile}/{catalogType}/Authentications/authenticateWithoutUserLookup`

### Rationale

This maps policy intent to the confirmed endpoint classification while requiring future non-GET actions to opt into reader access deliberately.

### Alternatives considered

- Classify every POST as administrator-only: rejected because the specification explicitly grants reader access to the two authentication endpoints.
- Infer policy from HTTP verb at runtime: rejected because explicit policy metadata is easier to audit and protects unusual routes.

## References

- [Duende API Resources](https://docs.duendesoftware.com/identityserver/fundamentals/resources/api-resources/)
- [Duende ASP.NET Core and API Access](https://docs.duendesoftware.com/identityserver/quickstarts/3-api-access/)
