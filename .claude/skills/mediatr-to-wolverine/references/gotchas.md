# Gotchas checklist

Walk through every item before declaring the migration done. Most of these compile fine and fail (or misbehave) only at runtime.

## Discovery

1. **Handlers in other assemblies not found.** Wolverine scans only the application assembly by default. Add `opts.Discovery.IncludeAssembly(...)` for each handler assembly or `[assembly: WolverineModule]`. Symptom: `IndeterminateRoutesException` / "no handler" at runtime.
2. **Non-public types.** `internal` handlers, messages or validators are skipped or break code generation. Clean Architecture templates often use `internal sealed class ...Handler` — make them `public`.
3. **Name doesn't match convention.** Class must end with `Handler`/`Consumer`; method must be `Handle`/`HandleAsync`/`Consume`/... Otherwise add `[WolverineHandler]` or rename.
4. **Accidental handlers.** Any public class ending in `Handler` with a public `Handle*`/`Consume*` method becomes a Wolverine handler for its first parameter type. Common false positives: ASP.NET Core `AuthorizationHandler` implementations exposing `public Task HandleAsync(AuthorizationHandlerContext)`, custom `ErrorHandler.Handle(Exception)`, `*EventHandler` classes for UI/other buses. Add `[WolverineIgnore]` to them. The inventory script lists suspects.
5. **Open generic handlers.** Not supported at all. Close them per message type.
6. **Test hosts** pick the wrong application assembly (see `testing.md`).

Diagnose any of these with `Console.WriteLine(opts.DescribeHandlerMatch(typeof(SomeHandler)));` inside `UseWolverine`, then remove the line.

## Invocation semantics

7. **Missing type argument.** `InvokeAsync(query)` instead of `InvokeAsync<T>(query)` returns nothing and *cascades* the result as a message.
8. **Type argument mismatch.** `InvokeAsync<T>` requires `T` to be the handler's return type exactly (e.g. handler returns `List<OrderDto>`, caller asks for `IReadOnlyList<OrderDto>` → failure). Fix the handler signature or the call.
9. **Tuple / `IEnumerable<object>` / `object` returns** are interpreted as multiple cascading messages, not a response. Wrap in a record.
10. **More than one handler for a request.** MediatR allowed exactly one `IRequestHandler` per request; Wolverine runs *all* discovered handlers for a message type. Leftover or duplicated handlers will both run.
11. **Zero handlers for a former notification** → error with `InvokeAsync` (MediatR no-op). See `notifications.md`.
12. **`DurabilityMode.MediatorOnly`** disables local queues: `PublishAsync`/cascading to local handlers stop working. Only use it when the app strictly uses `InvokeAsync`.
13. **Retries on inline invocation.** Wolverine error policies with retries also apply to `InvokeAsync`. A global `opts.OnException<SqlException>().RetryTimes(3)` can re-execute a non-idempotent handler during an HTTP request.
14. **Timeout.** `InvokeAsync` accepts an optional timeout; confirm long-running handlers (reports, imports) aren't cut short.
15. **Validate returning `Stop`** makes `InvokeAsync<T>` return without an exception — use exceptions where the old code threw (404/403/400 mapping).

16. **`SendAsync` with no known route throws**, whereas `PublishAsync` silently drops the message. Neither reaches local handlers under `DurabilityMode.MediatorOnly`.
17. **Static middleware as a generic argument.** `opts.Policies.AddMiddleware<SomeStaticClass>()` is a compile error (CS0718); use `AddMiddleware(typeof(SomeStaticClass), ...)`.

## Validation

18a. **Double validator registration.** If the Application project calls `services.AddValidatorsFromAssembly(...)`, `opts.UseFluentValidation()` registers them again; use `RegistrationBehavior.ExplicitRegistration` or remove one registration.
18b. **Validators and handlers in one file/assembly.** Validators must be `public`, and the Application assembly must be included for discovery even if the validators are only registered through FluentValidation's own scan.

## Logging and secrets

18c. **Failed handlers log the message.** When a handler (or validation) throws, Wolverine logs `Invocation of <message.ToString()> failed!` at Error level. A record that carries a secret as a plain `string` (e.g. `SetMsAdUserPasswordCommand.NewPassword`) now writes it **in clear text** to the logs. MediatR behaviors typically logged only the request name, so this is a silent regression. Verified fix: override `PrintMembers` in the record (`private bool PrintMembers(StringBuilder b)`) to mask the member, or type the member as a masking value object. Search every message for password/token/secret/key members and add a test that a failing command does not log the value.
18d. **Extra Error log on exceptions.** The same failure log duplicates what a global exception middleware or an `UnhandledExceptionBehavior` logged; expect two error entries per exception (and a changed message format) unless the Wolverine log is tuned. Report it as a runtime change.

## Completeness

18. **Shims left behind.** `using Wolverine.Shims.MediatR;` keeps `IRequest`/`IRequestHandler` in the code. The migration must be complete: remove them and the interfaces. `inventory.py --check` fails while any remain.
19. **MediatR-shaped wrappers.** A custom `IMediator`/`ISender` over `IMessageBus`, or a single global middleware that re-implements every old behavior with runtime type checks, recreates MediatR under another name. Replace with direct `IMessageBus` usage and selectively applied middleware.
20. **Wolverine in the Domain project.** Domain entities and domain events must not reference `WolverineFx`.

## DI and lifetimes

21. **DI scope per invocation.** Each `InvokeAsync` builds its own scope for the handler's dependencies; nested `InvokeAsync` calls from inside a handler don't share the outer `DbContext` the way MediatR handlers sharing the request scope did. Pass data explicitly or restructure.
22. **Lambda-registered services** (`AddScoped(sp => ...)`) force service location in generated code. It works; Wolverine may log warnings. Fine for migration; optimise later.
23. **`DbContext` options lifetime.** `AddDbContext<T>(..., optionsLifetime: ServiceLifetime.Singleton)` lets Wolverine generate more efficient code. Optional.

## Startup and production

23a. **Runtime compiler package (Wolverine 6.x).** Core `WolverineFx` no longer ships Roslyn: with the default `TypeLoadMode.Dynamic` the host throws `InvalidOperationException: Wolverine is running in TypeLoadMode.Dynamic ... no IAssemblyGenerator` at startup. Add `WolverineFx.RuntimeCompilation` (same version, to the host) or pre-generate with `codegen write` + `TypeLoadMode.Static`. Verified: with the package added, `WebApplicationFactory<Program>` tests start normally.
24. **Code generation at startup.** Wolverine generates and compiles handler adapters at runtime by default, adding first-call/startup latency. For production, consider pre-generating (`dotnet run -- codegen write`) with `opts.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto/Static`. Not required for correctness; mention it in the report.
25. **Command-line integration.** `codegen`/`describe` commands need the host to end with `return await app.RunJasperFxCommands(args);` (older: `RunOaktonCommands`). Optional, but very useful for verifying middleware.
26. **Package version skew.** All `WolverineFx.*` packages must share one version.

## Things with no direct equivalent (report to user)

27. `IStreamRequest<T>` / `CreateStream` → no mediator streaming; use a service method returning `IAsyncEnumerable<T>` or Wolverine.HTTP streaming.
28. `IRequestExceptionHandler` that sets a fallback response.
29. Caching behaviors that short-circuit.
30. Custom `INotificationPublisher` strategies (parallel publish).
31. Third-party libraries that depend on MediatR (some audit, outbox, or DDD base libraries) — they keep pulling MediatR in transitively; the `--check` step catches the `PackageReference` but not transitive use, so run `dotnet list package --include-transitive | grep -i mediatr` too.
