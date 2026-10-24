# Walkthrough: Uniform domain events for profile and catalog handlers

Run: run-bitai-ldapgateway-007 | Intent: code-review-20251023-remediation

## Changes

- `OperationCompletedDomainEvent`: `ServerProfile` and `CatalogType` are optional, because configuration reads have no catalog (and some no profile). Avoids writing fake catalog values into the audit trail.
- `LdapHandlerBase.ExecuteWithoutCatalogAsync`: publishes the event without a catalog.
- `GetProfileById`, `GetProfileIds`, `GetProfiles`, `GetCatalogTypes` handlers inherit `LdapHandlerBase`.
- `WolverineDomainEventPublisher`: unknown events log a warning with the event type.
- `src/Api/Internal/Generated/WolverineHandlers/*`: regenerated (constructor wiring only), committed with the change as the constitution requires.

## Operational note

The notification log line now prints an empty `Catalog=` for these four operations.

To regenerate again after changing handler or publisher constructors: delete `src/Api/Internal/Generated/WolverineHandlers`, `dotnet build src/Api -c Debug`, then `dotnet run --no-build --project src/Api -c Debug -- codegen write` (stale generated code otherwise blocks compilation).

## Verify

`dotnet test Bitai.LDAPGateway.slnx` and `dotnet build src/Api -c Release`.
