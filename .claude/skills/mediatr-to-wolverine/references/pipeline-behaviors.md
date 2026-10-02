# Porting pipeline behaviors, pre/post processors and exception handlers

## Contents
1. Mental model
2. Decision table by behavior purpose
3. Generic behavior → conventional middleware (recipe)
4. Validation (FluentValidation)
5. Logging / timing
6. Transactions / unit of work
7. Authorization
8. Caching and other short-circuit behaviors
9. Pre/post processors
10. Exception handlers and actions
11. Ordering and verification

---

## 1. Mental model

MediatR behaviors are runtime "Russian doll" wrappers: each `Handle(request, next, ct)` calls `next()` and can do work before, after, around (try/catch/finally), or **instead of** the handler, and can replace the response.

Wolverine middleware is woven into generated code around the handler:

```
Before/Load/Validate   → can stop the chain (HandlerContinuation.Stop) or produce values for later methods
handler
After / PostProcess    → runs after the handler, BEFORE any transactional commit
AfterCommit            → runs after commit + outbox flush (skipped if commit throws)
Finally                → in a finally block, always runs
```

Before methods can return objects that are then injectable into the handler and into After/Finally (that's how state crosses "around" the handler, e.g. a `Stopwatch`). Method name matching is case-sensitive.

What middleware **cannot** do as directly as a behavior: substitute a different response value for the caller of `InvokeAsync<T>`, or catch-and-swallow exceptions from the handler. Those cases are covered in §8 and §10.

---

## 2. Decision table

First decide **where** each behavior belongs. A MediatR behavior ran for every request because that was MediatR's only hook; in the migrated solution it should live at the layer that owns the concern:

| Destination | Use when the concern… | Typical behaviors |
|---|---|---|
| Built-in Wolverine add-on / feature | is already provided by Wolverine | FluentValidation, transactions (EF Core), retries, message logging/metrics |
| Wolverine middleware (selective) | applies to message handling, for a known set of message types | authorization by message type, tenant enrichment, audit of commands |
| ASP.NET Core middleware / `IExceptionHandler` | is about the HTTP request as a whole, whatever handler runs | exception → ProblemDetails mapping, correlation IDs, request logging |
| Endpoint / MVC filter | applies to specific HTTP endpoints, not to messages | per-endpoint authorization, model-state checks, output caching |
| Domain logic (inside the handler or entity) | is a business rule that happened to be placed in a behavior | invariants, "order must be open" checks |
| Infrastructure concern (a service/decorator) | wraps a dependency rather than the message pipeline | caching a repository, Polly around an HTTP client |
| Delete | duplicates something Wolverine or ASP.NET Core already does | "Handling X / Handled X" logging, try/catch-log-rethrow |

Then, for the ones that stay in the message pipeline:

| Behavior purpose (typical name) | Port to |
|---|---|
| FluentValidation (`ValidationBehavior`) | `WolverineFx.FluentValidation` → `opts.UseFluentValidation()` (§4) |
| DataAnnotations validation | `WolverineFx.DataAnnotationsValidation` add-on, or a `Validate` method |
| Logging request/response (`LoggingBehavior`) | Wolverine built-in message logging, or Before/Finally middleware (§5) |
| Performance timing (`PerformanceBehavior`) | Built-in metrics, or Before/Finally middleware with a `Stopwatch` (§5) |
| `TransactionBehavior` / `UnitOfWorkBehavior` | `[Transactional]` / `AutoApplyTransactions()` with EF Core integration, or Before/After middleware (§6) |
| Authorization (`AuthorizationBehavior`) | Before middleware that throws or stops (§7) |
| Caching (`CachingBehavior`) | move to call site or into handler `Load` (§8) |
| Idempotency / dedup | Wolverine's durable inbox / idempotency features, or Before middleware |
| Exception logging (`UnhandledExceptionBehavior`) | Wolverine already logs failures; or call-site/global exception handling (§10) |
| Retry (Polly inside behavior) | Wolverine error-handling policies (`opts.OnException<T>()...`) |
| Tenant / user context enrichment | Before middleware taking `IMessageContext`/`Envelope` or the app's context service |

---

## 3. Generic behavior → conventional middleware (recipe)

```csharp
// Before
public class AuditBehavior<TRequest, TResponse>(IAuditLog audit, ICurrentUser user)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        await audit.StartAsync(typeof(TRequest).Name, user.Id, ct);
        try
        {
            var response = await next();
            await audit.SucceededAsync(ct);
            return response;
        }
        finally
        {
            await audit.EndAsync(ct);
        }
    }
}

// After
public class AuditMiddleware(IAuditLog audit, ICurrentUser user)
{
    public Task BeforeAsync(Envelope envelope, CancellationToken ct)
        => audit.StartAsync(envelope.MessageType ?? "unknown", user.Id, ct);

    public Task AfterAsync(CancellationToken ct) => audit.SucceededAsync(ct);    // only on success

    public Task FinallyAsync(CancellationToken ct) => audit.EndAsync(ct);        // always
}

// Program.cs
opts.Policies.AddMiddleware<AuditMiddleware>();
```

Recipe:
1. Code before `await next()` → `Before`/`BeforeAsync`.
2. Code after `next()` on the success path → `After`/`AfterAsync` (or `AfterCommit` if it must only happen once data is durable).
3. Code in `finally` → `Finally`/`FinallyAsync`.
4. Code in `catch` → see §10.
5. Values shared across those sections → return them from `Before` and accept them as parameters later.
6. `typeof(TRequest)` → inject `Envelope` and use `envelope.MessageType`, or take the message itself.
7. Behaviors constrained to a marker (`where TRequest : ICommand`) → `opts.Policies.ForMessagesOfType<ICommand>().AddMiddleware(typeof(XMiddleware))` and make the middleware's first parameter the marker type (Wolverine treats the first parameter as the message type for matching).
8. Behaviors applied broadly → `opts.Policies.AddMiddleware<T>()` for an **instance** class, optionally with a filter: `AddMiddleware<T>(chain => chain.MessageType.IsInNamespace("MyApp.Commands"))`. Prefer a filter or `ForMessagesOfType<T>()` over applying to everything — selective application is the point of Wolverine middleware.
9. Behaviors applied to a few handlers → `[Middleware(typeof(XMiddleware))]` on the handler class/method.
10. Middleware classes may be static (fewer allocations) or instance with constructor injection. **A static class cannot be a generic type argument** (C# error CS0718), so `opts.Policies.AddMiddleware<StaticMiddleware>()` does not compile. Register static middleware with the `Type`-based overloads: `opts.Policies.AddMiddleware(typeof(StaticMiddleware), chain => ...)`, `ForMessagesOfType<T>().AddMiddleware(typeof(StaticMiddleware))`, or `[Middleware(typeof(StaticMiddleware))]`.
11. Middleware applies to message handlers; Wolverine.HTTP endpoints have their own middleware registration.

---

## 4. Validation (FluentValidation)

```xml
<PackageReference Include="WolverineFx.FluentValidation" Version="<same as WolverineFx>" />
```

```csharp
opts.UseFluentValidation();   // applies middleware AND scans/registers validators
// or, if validators are registered elsewhere already:
opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration);
```

- On failure it throws `FluentValidation.ValidationException` — the same exception most MediatR `ValidationBehavior`s throw, so existing exception-to-ProblemDetails mapping usually keeps working. Confirm by checking what the old behavior threw; if it threw a **custom** exception type, register an `IFailureAction<T>` (singleton) that throws that type:

```csharp
public class ThrowAppValidationException<T> : IFailureAction<T>
{
    public void Throw(T message, IReadOnlyList<ValidationFailure> failures)
        => throw new AppValidationException(failures);
}
opts.Services.AddSingleton(typeof(IFailureAction<>), typeof(ThrowAppValidationException<>));
```

- Middleware only attaches to message types that have registered validators.
- Don't double-register validators (remove `AddValidatorsFromAssembly` or use `ExplicitRegistration`).
- Validators must be **public**, or enable `IncludeInternalTypes` and accept the lifetime caveats.
- Validators with scoped dependencies can force service-location in generated code; that works but is slower — fine for a migration.
- The add-on also adds an error policy that discards messages failing validation when they arrive via queues; for `InvokeAsync` the exception propagates to the caller.

---

## 5. Logging / timing

Wolverine logs message execution and emits metrics/OpenTelemetry out of the box. If the old behavior only logged "Handling X / Handled X in N ms", consider deleting it and setting `opts.InvokeTracing = InvokeTracingMode.Full` (newer versions) so `InvokeAsync` calls get the same structured logs as queued messages. If the exact log format matters (dashboards, alerts), port it:

```csharp
public static class TimingMiddleware
{
    public static Stopwatch Before() => Stopwatch.StartNew();

    public static void Finally(Stopwatch sw, ILogger logger, Envelope envelope)
    {
        sw.Stop();
        logger.LogInformation("Handled {MessageType} in {Elapsed} ms", envelope.MessageType, sw.ElapsedMilliseconds);
    }
}
```

```csharp
// TimingMiddleware is static, so use the Type-based overload (AddMiddleware<TimingMiddleware>() won't compile)
opts.Policies.AddMiddleware(typeof(TimingMiddleware), chain => chain.MessageType.IsInNamespace("MyApp.Commands"));
```

`ILogger` (non-generic) is supplied by Wolverine as `ILogger<MessageType>`.

**Behaviors that inspect the response** (Result pattern: `where TResponse : Result`, log success vs. failure, walk `Error.InnerError`). Move the formatting into a plain helper (`ResultLogger.Log(ILogger, Result)`) and call it from an `After` method whose parameter is the **base** response type (`After(Result result, ILogger logger, Envelope envelope)`). Wolverine binds an `After` parameter to the handler's return value only if the types are compatible, so confirm with `codegen preview` / a log-capturing test that it fires for `Result` **and** `Result<T>` handlers. If binding fails, a message-specific `After` per response type, or logging inside the shared handler base class (e.g. `ExecuteAsync`), is the fallback. Don't drop the failure logging silently: report which option was used.

---

## 6. Transactions / unit of work

Typical MediatR behavior: begin transaction → `next()` → `SaveChangesAsync` → commit.

**Decide the boundary first, once for the whole solution, and record it in the final report.** Options:

| Boundary | Means | Choose when |
|---|---|---|
| HTTP request | one transaction per request, possibly spanning several handlers | controllers invoke several commands that must succeed or fail together |
| Message handler | one transaction per handled message (Wolverine transactional middleware) | each command is its own unit of work — the usual case and the closest to a per-request MediatR behavior |
| Database transaction + outbox | the handler's writes and its outgoing messages commit atomically | handlers publish events/commands that must not be lost or sent for rolled-back data |
| Domain operation | the aggregate/repository owns `SaveChanges` | the domain layer already controls persistence explicitly |

Check what the old behavior actually did — which requests it applied to (all, or only `ICommand`), and whether it published notifications before or after commit — and pick the boundary that matches it, unless the user approves a change. Moving to "database transaction + outbox" is the main reliability upgrade Wolverine offers; recommend it in the report if notifications or integration events are published from handlers.

Preferred port for a per-handler boundary (EF Core):

```csharp
// WolverineFx.EntityFrameworkCore
opts.UseEntityFrameworkCoreTransactions();
opts.Policies.AutoApplyTransactions();   // or [Transactional] on specific handlers
```

Wolverine then calls `SaveChangesAsync` after the handler and, if message persistence is configured, enrolls outgoing messages in the outbox. If the old behavior only applied to commands (`where TRequest : ICommand`), use `[Transactional]` on command handlers instead of `AutoApplyTransactions()` (which applies to any handler that uses a recognized persistence service).

Fallback port (keep exact semantics, no outbox): Before middleware begins the transaction and returns it; After commits; Finally disposes. Never call `SaveChangesAsync` in both the handler and the middleware unless the original code did.

Side effects that the old behavior ran *after commit* (publishing integration events, cache invalidation) belong in `AfterCommit`, not `After` — `After` runs **before** the commit.

---

## 7. Authorization

```csharp
public class AuthorizationMiddleware(ICurrentUser user, IAuthorizationService authz)
{
    public async Task BeforeAsync(IRequireAuthorization message, CancellationToken ct)
    {
        if (!await authz.IsAllowedAsync(user, message.Policy, ct))
            throw new ForbiddenAccessException();   // same exception the behavior threw
    }
}
opts.Policies.ForMessagesOfType<IRequireAuthorization>().AddMiddleware(typeof(AuthorizationMiddleware));
```

If authorization was attribute-driven (`[Authorize(Roles = "...")]` on the request class read via reflection in the behavior), keep the attribute and read it in `Before` via `message.GetType()` — or, better, write a policy (`IHandlerPolicy`) that only attaches the middleware to chains whose message type has the attribute. Throwing preserves the old contract (callers/exception filters expecting an exception); returning `HandlerContinuation.Stop` would make `InvokeAsync<T>` return without an error — avoid that for authorization.

---

## 8. Caching and other short-circuit behaviors

A behavior that returns a cached response **without calling `next()`** has no direct middleware equivalent, because middleware cannot hand a substitute response back to `InvokeAsync<T>`. Options, in order of preference:

1. Move caching into the handler: a `LoadAsync` that checks the cache and a `Handle` that uses the loaded value or queries and populates the cache.
2. Move caching to the call site / a small query service wrapping `IMessageBus`.
3. Use HTTP output caching if the query is only reached via HTTP.

Report to the user which queries were affected.

---

## 9. Pre/post processors

- `IRequestPreProcessor<T>.Process(request, ct)` → `Before`/`BeforeAsync(T request, ...)` in a middleware class registered with `ForMessagesOfType<T>()` or globally.
- `IRequestPostProcessor<T, TResponse>.Process(request, response, ct)` → `After`/`AfterAsync`. To read the handler's response in `After`, take a parameter of the response type; verify the generated code (`codegen preview`) that it's bound to the handler's return value. If the post-processor must run only after the database commit, use `AfterCommit`.

---

## 10. Exception handlers and actions

- `IRequestExceptionAction<TRequest, TException>` (side effect, exception still propagates): exceptions from `InvokeAsync` propagate to the caller, so put the side effect in the caller's catch, the global ASP.NET Core exception handler (`IExceptionHandler`/`UseExceptionHandler`), or a Wolverine error policy (`opts.OnException<TException>()...`) for queued messages.
- `IRequestExceptionHandler<TRequest, TResponse, TException>` that **sets a handled response** (swallows the exception and returns a fallback): no middleware equivalent. Move the try/catch into the handler itself, or to the call site. Flag each one in the final report.
- Behaviors wrapping `next()` in try/catch to log and rethrow: delete (Wolverine logs failures) or keep the logging in the global exception handler.
- Polly retries in a behavior: Wolverine error handling policies (retry, retry-with-cooldown) apply to `InvokeAsync` inline execution too. **Caution:** global retry policies can re-run a handler invoked from an HTTP request; make sure the handler is idempotent or scope the policy to specific exception/message types.

---

## 11. Ordering and verification

- MediatR runs behaviors in registration order (outermost first). Wolverine applies middleware in the order policies are added. Register middleware in the same order the behaviors were registered.
- Inspect the generated code for one representative message to confirm order and which middleware attached:
  - `dotnet run -- codegen preview` (requires the host to end with `return await app.RunJasperFxCommands(args);` — older versions: `RunOaktonCommands`), or
  - `dotnet run -- describe`.
- Add one test per ported behavior proving it still fires (invalid command rejected, unauthorized user forbidden, transaction rolled back on failure, timing log emitted). A ported behavior with no test is the most likely place for a silent regression.
