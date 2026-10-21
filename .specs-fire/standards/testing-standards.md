# Testing Standards

- Framework: xUnit with Moq. Projects: `Domain.UnitTests`, `Application.UnitTests`, `Api.FunctionalTests` (WebApplicationFactory).
- Tests must run without a live directory; use the mock adapter in CI.
- Every new use case has at least one test at its boundary (handler/endpoint).
- Run: `dotnet test Bitai.LDAPGateway.slnx`
- Build must be warning-free before tests are considered passing.
- Generated artifacts (e.g. OpenAPI) are committed with the change that produced them.
