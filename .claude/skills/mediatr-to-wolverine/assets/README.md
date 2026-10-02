# Templates (copy, then adapt)

These are the pieces that had to be written by hand in the verified migration (Wolverine 6.44).
They are `.txt` so they are never compiled in place. Copy each into the solution, rename to `.cs`,
and replace the `YourApp` namespaces and the `// ADAPT:` lines. Keep the class names and the
`Application/Common/Behaviors` location so migrations stay comparable.
Delete every `// ADAPT:` marker once the line is adapted; they must not survive in the migrated code.

| File | Goes to | Replaces |
|---|---|---|
| `ResultLoggingMiddleware.cs.txt` | `<Application>/Common/Behaviors/ResultLoggingMiddleware.cs` | a `LoggingBehavior` that branches on `Result` |
| `ResultLoggingPolicy.cs.txt` | `<Application>/Common/Behaviors/ResultLoggingPolicy.cs` | (needed because `After(Result)` does not bind) |
| `Program.Wolverine.cs.txt` | host `Program.cs` | `AddMediatR` + behavior registrations |
| `MaskSecrets.cs.txt` | the message records that carry secrets | (new: Wolverine logs failed messages) |
| `CapturedLogs.cs.txt` | `<Api.FunctionalTests>/CapturedLogs.cs` | (test helper) |
| `BehaviorRegressionTests.cs.txt` | `<Api.FunctionalTests>/MigrationBehaviorTests.cs` | (new regression tests) |

Only use the logging pair when the old behavior logged success/failure from a `Result`. If no such
behavior existed, skip them.
