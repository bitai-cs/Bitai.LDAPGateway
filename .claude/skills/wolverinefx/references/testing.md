# Testing a Wolverine solution

Read when: writing or reviewing tests for handlers, middleware, host startup, tracked sessions, transports or the outbox.

Tests mirror the layers. Everything labelled "verified" was run against WolverineFx 6.44.0 (xUnit, a class library with no Wolverine reference hosted by a project that calls `UseWolverine`); APIs and names can change between versions, so check them against the version in use.

| Test project | What it tests | Wolverine in the test project |
| --- | --- | --- |
| Domain.UnitTests | entities, value objects, aggregates, domain events | none |
| Application.UnitTests | handlers (instance or static), validators, plain middleware methods; no host | none (only under the pragmatic profile, if the handlers use Wolverine types) |
| Infrastructure.IntegrationTests | persistence, outbox, real transports | `WolverineFx` |
| Api / host integration tests | endpoints, host startup, discovery, cascades | `WolverineFx` (the tracking helpers live in the core package, namespace `Wolverine.Tracking`) |

### Handler unit tests (no host)

Handlers are plain classes, so call them directly. Instance handlers get fakes for their Application abstractions; static handlers are pure functions that receive their dependencies as parameters. Both were verified:

```csharp
var (reply, placed) = new PlaceOrderHandler().Handle(new PlaceOrder(5));   // instance handler
var pong = PingHandler.Handle(new Ping("a"));                              // static handler
```

- A handler that returns its effects (a tuple such as `(OrderReply, OrderPlaced)`) is asserted on its return value; do not mock the bus to check what it emits.
- Middleware written as plain methods is unit tested by calling the method directly (e.g. a `LogResult(Result, ILogger)` method with a fake logger).

### Integration tests with tracked sessions

Messages run asynchronously, so integration tests wait with tracked sessions (`using Wolverine.Tracking;`), never with `Task.Delay` or sleeps. Verified:

- `host.InvokeMessageAndWaitAsync(message)` runs the handler inline and waits for the whole cascade; `host.SendMessageAndWaitAsync(message)` goes through the normal pipeline.
- `host.TrackActivity().Timeout(TimeSpan.FromSeconds(10)).ExecuteAndWaitAsync(...)` for a custom session. Cast an async lambda explicitly, `(Func<IMessageContext, Task>)(async ctx => ...)`: `ExecuteAndWaitAsync` also accepts a `ValueTask` delegate and the plain lambda is ambiguous.
- Assert on the session: `session.Sent.SingleMessage<OrderPlaced>()`, `session.Executed.SingleMessage<T>()`, `session.Scheduled.SingleMessage<T>()`. The session also exposes `Received`, `MessageSucceeded`, `MessageFailed`, `NoHandlers` and `NoRoutes`.
- A handler that throws makes the tracked call throw (verified with `SendMessageAndWaitAsync`: an `AggregateException` carrying the handler's exception), so the test fails by default. For an expected failure use `TrackActivity().DoNotAssertOnExceptionsDetected()` and assert on `session.AllExceptions()`.
- Sending a message with no handler or route throws `IndeterminateRoutesException`, even with `DoNotAssertOnExceptionsDetected`.
- `DoNotAssertOnTimeout()` exists (XML docs): use it only to assert that something does NOT happen. A timeout normally means a message was never processed (no handler, wrong route), not that the timeout is short.
- A scheduled message is asserted through `session.Scheduled`, without waiting for the real delay (verified with `ctx.ScheduleAsync(message, TimeSpan.FromMinutes(30))`). Abstract time behind `TimeProvider` when your own logic depends on it.

### Isolating transports and durability in tests

- `services.RunWolverineInSoloMode()` and `services.DisableAllExternalWolverineTransports()` (extensions on `IServiceCollection`) start cleanly and tracked sessions work with both (verified). The XML docs say the second one is meant for integration tests that must not leave the process. `opts.StubAllExternalTransports()` exists for the same purpose when a transport package is configured (XML docs).
- Do NOT use `DurabilityMode.MediatorOnly` in tests that use tracked sessions: `TrackActivity` throws `InvalidOperationException: This operation is not allowed with Wolverine is bootstrapped in MediatorOnly mode` (verified).
- Keep two levels: most tests with transports disabled or stubbed (fast) and a small separate set against the real broker in a container, enabling external activity explicitly with `TrackActivity().IncludeExternalTransports()`.

### Host and HTTP tests

- Bootstrap the real application with `WebApplicationFactory<Program>` or Alba so Wolverine resolves the correct application assembly and handler discovery. The host needs `public partial class Program;` and, in Debug, `WolverineFx.RuntimeCompilation` (`codegen-production.md`). In test processes that start several Wolverine hosts, set `opts.ApplicationAssembly` explicitly or include the handler assembly, because discovery can otherwise silently differ between hosts.
- Reuse one factory per class or collection fixture: starting the host is expensive.
- `JasperFxEnvironment.AutoStartHost` (`JasperFx.CommandLine`) was not needed: a `WebApplicationFactory<Program>` host started Wolverine exactly once with and without it. Add it only if a double start is observed.
- To assert logs (success, failure, secrets not leaked) add an `ILoggerProvider` that implements `ISupportExternalScope` and expand dictionary scopes when printing them (a `Dictionary` scope prints its type name). Tests must not depend on git-ignored local configuration such as `appsettings.Development.json`: discover valid ids through the API or override configuration in the test host. Drop the log entries produced by setup requests before asserting that something did not happen.

### Persistence and outbox

- Integration tests that touch persistence or the outbox use xUnit with Testcontainers and a real database engine matching production, not an in-memory substitute, so the transactional behavior is real.
- Isolate state: each test creates its own data (unique ids) or the database is cleaned between tests; the outbox, in-memory queues and the database persist across tests otherwise. Test classes that share a database run in the same xUnit collection (no parallelism between them). Apply EF Core migrations or create the schema before the host starts.

### Host startup and discovery

Test that the host boots and that every message type has a handler: it catches a missing `IncludeAssembly(...)` immediately. `IWolverineRuntime` does not expose the handler graph; the concrete `WolverineRuntime` does (verified):

```csharp
using Wolverine.Runtime;
var graph = ((WolverineRuntime)host.Services.GetRequiredService<IWolverineRuntime>()).Handlers;
Assert.True(graph.CanHandle(typeof(PlaceOrder)));      // also graph.AllMessageTypes(), graph.Chains
```

The negative case was verified too: a host without `IncludeAssembly` for the handler assembly reports `CanHandle(typeof(PlaceOrder)) == false`. Enumerate the command and query types of the Application assembly and assert each one.

### What a Wolverine solution must always test

1. The host boots and discovers a handler for every command and query (host startup and discovery above).
2. Each cascading flow and scheduled message through a tracked session (tracked sessions above).
3. An invalid command is rejected (400 at the HTTP edge) and its handler does not run (SKILL.md section 3).
4. Failures returned as `Result` values are logged by the policy-attached method, if the codebase uses a Result type (`logging-and-results.md`).
5. A failing command does not write secrets to the logs (`logging-and-results.md`).
6. A message with no handler fails with `IndeterminateRoutesException`.
7. Generated code is up to date: CI regenerates and fails on any change (`codegen-production.md`).
8. The confirmed Wolverine profile (SKILL.md section 1) with an ArchUnitNET test: Domain does not reference Wolverine; under purist Application does not either, under pragmatic it references no Wolverine package other than the core one; Domain does not reference Application, Infrastructure or Presentation; handlers are public and end with `Handler`.
