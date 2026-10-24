# Code Review: run-bitai-ldapgateway-007

- Overload named `ExecuteWithoutCatalogAsync` instead of a second `ExecuteAsync`: a literal `null` profile made the call ambiguous with the `LdapRequestContext` overload, and the name states the intent.
- `OperationCompletedDomainEvent.ServerProfile`/`CatalogType` are nullable; existing callers and tests compile unchanged.
- Generated Wolverine code was regenerated with `codegen write` after deleting the stale folder (the stale code no longer compiled, since publisher and handler constructors changed). 18 generated files changed, all constructor-argument wiring; no file added or removed.
- Publisher warns on unknown events rather than throwing, so an audit hook cannot fail a user request.
- No issues found; no auto-fixes applied.
