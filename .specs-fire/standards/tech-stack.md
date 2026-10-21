# Tech Stack

## Runtime
- .NET 10 (`net10.0`), C#, nullable reference types and implicit usings enabled
- ASP.NET Core Web API (controllers)

## Libraries
- WolverineFx 6.44 (in-process mediator; `WolverineFx.FluentValidation` middleware; `WolverineFx.RuntimeCompilation` in Debug only)
- FluentValidation 12.1
- NSwag.AspNetCore 14.7 (OpenAPI)
- Microsoft.Extensions.* (Http, Options, Logging abstractions) 10.0
- Bitai.LDAPHelper (LDAP access; XML docs in `docs/`)

## Persistence
- None. The gateway is stateless; the directory is the source of truth.

## Configuration
- LDAP server profiles are static entries in `appsettings.json`, validated at startup.

## Tooling
- Solution: `Bitai.LDAPGateway.slnx`
- Tests: xUnit 2.9, Moq 4.20, Microsoft.AspNetCore.Mvc.Testing, coverlet
- `.editorconfig` at root (4 spaces, LF, UTF-8)
