---
name: mediatr-to-wolverine
description: Migrate a .NET solution from MediatR to Wolverine (WolverineFx). Use this skill whenever the user wants to replace, remove, or move off MediatR, convert IRequest/IRequestHandler/INotificationHandler/IPipelineBehavior code to Wolverine handlers and middleware, swap IMediator/ISender/IPublisher for IMessageBus, or asks how MediatR concepts map to Wolverine — even if they only mention "mediator replacement", "MediatR license", or "Wolverine as mediator".
---

# MediatR → Wolverine migration

This skill walks an agent through converting a .NET solution that uses MediatR so that it uses Wolverine instead, safely and verifiably.

**The migration is always complete, in one pass.** The end state is native Wolverine: no MediatR packages, no MediatR interfaces, no `Wolverine.Shims.MediatR`, and no home-grown `IMediator`-style wrapper around `IMessageBus`. There are no transitional phases and no "finish it later" items, except the ones listed under *Reporting back* that need a human decision.

Within that, preserve runtime behaviour by default: a migration that silently changes when handlers run, what they return, or whether their exceptions reach the caller is worse than no migration. Every behaviour change must be a deliberate, reported decision.

Primary reference: https://wolverinefx.net/introduction/from-mediatr.html (Wolverine docs also publish an LLM-friendly dump at https://www.wolverinefx.io/llms-full.txt — fetch the relevant section when an API detail here is in doubt; Wolverine releases often).

## Files in this skill

| File | Read it when |
|---|---|
| `scripts/inventory.py` | Step 1 (always). Scans the solution and reports every MediatR touch-point; `--check` mode is the final gate. |
| `references/api-mapping.md` | Converting requests, handlers, call sites, DI registration. Before/after code for each MediatR construct. |
| `references/pipeline-behaviors.md` | The inventory shows `IPipelineBehavior`, pre/post processors, or exception handlers/actions. |
| `references/notifications.md` | The inventory shows `INotification`, `INotificationHandler`, or `Publish(...)` calls. Read before touching them — semantics differ. |
| `references/testing.md` | Updating unit/integration tests and mocks of `IMediator`/`ISender`. |
| `references/gotchas.md` | Before declaring done, and whenever something "builds but doesn't run". |

## Workflow

Work through these steps in order. Keep the solution building between steps where you can; commit (or tell the user to commit) after each step that compiles so the migration is reviewable and reversible.

### 1. Inventory

Run the scanner from the solution root:

```bash
python3 <skill-dir>/scripts/inventory.py <solution-root> --format markdown > mediatr-inventory.md
```

It lists: projects referencing MediatR packages, request/notification types, handlers (including open-generic ones), pipeline behaviors, pre/post processors, exception handlers, stream requests, `Send`/`Publish`/`CreateStream` call sites, `AddMediatR` registrations, and test doubles of `IMediator`/`ISender`/`IPublisher`. Read the report fully before changing anything — the counts determine which reference files you need and how big the job is. Share a short summary with the user (counts per category, anything flagged ⚠).

### 2. Choose the handler style

Both styles below produce a **complete** migration to native Wolverine conventions; they differ only in how much each handler's body is reshaped. Pick one for the whole solution.

- **A. Conservative (default).** Native Wolverine handlers that keep their classes, constructor injection and method bodies. Lowest risk, easy to review.
- **B. Idiomatic.** Additionally convert handlers to static methods with method injection, use cascading return values instead of `IPublisher`, compound handlers (`Load`/`Validate`/`Handle`), and optionally replace thin controller → `Send()` endpoints with Wolverine.HTTP endpoints. Less code, but every handler body is rewritten.

If the user hasn't said, ask once. If they aren't available, do **A** and state that choice at the top of your summary. Never do B silently — it changes every file and makes review hard.

Do not use, in either style:
- **`Wolverine.Shims.MediatR`.** It keeps `IRequest`/`IRequestHandler` alive under a Wolverine namespace (and doesn't cover notifications or behaviors anyway). The final check fails if any shim usage remains.
- **A wrapper that imitates MediatR** (`IMediator`/`ISender` reimplemented over `IMessageBus`, a generic `IHandler<T>` base, generic "behaviors" re-created as one catch-all middleware). Call `IMessageBus` directly and apply middleware selectively.

Keep Wolverine out of the Domain project: domain entities, value objects and domain events must not reference `WolverineFx`. Messages and handlers belong in the Application layer.

### 3. Packages

For each project in the inventory:

- Remove `MediatR`, `MediatR.Contracts`, `MediatR.Extensions.Microsoft.DependencyInjection`, and MediatR-specific add-ons (e.g. FluentValidation behavior packages built for MediatR).
- Add `WolverineFx` to the **host** project (the one with `Program.cs`). Projects that only *contain* handlers/messages usually need no package at all, because Wolverine handlers need no interfaces; add `WolverineFx` there only if they use Wolverine types (`IMessageBus`, `HandlerContinuation`, attributes, middleware).
- Add as needed: `WolverineFx.FluentValidation` (FluentValidation behavior existed), `WolverineFx.Http` (+ `WolverineFx.Http.FluentValidation`) only if going HTTP-endpoint route, `WolverineFx.EntityFrameworkCore` if a transaction behavior wrapped EF Core `SaveChanges`.
- Use the latest stable version and keep all `WolverineFx.*` packages on the **same version** (check with `dotnet list package` or NuGet; if central package management is used, edit `Directory.Packages.props`).

```bash
dotnet remove <proj> package MediatR
dotnet add <host-proj> package WolverineFx
```

### 4. Bootstrapping

Replace `services.AddMediatR(...)` with Wolverine registration in the host:

```csharp
builder.Host.UseWolverine(opts =>
{
    // Handlers outside the host assembly are NOT scanned by default — see below.
    opts.Discovery.IncludeAssembly(typeof(SomeApplicationLayerType).Assembly);

    // Only if the app uses Wolverine purely as an in-process mediator
    // AND no notifications are published asynchronously (see notifications.md):
    // opts.Durability.Mode = DurabilityMode.MediatorOnly;
});
```

`builder.Services.AddWolverine(opts => ...)` is the equivalent for apps that configure via `IServiceCollection`; check which the installed version exposes.

**Assembly discovery is the #1 migration bug.** MediatR registration usually passes the Application/Core assembly explicitly; Wolverine scans only the *application assembly* (where `UseWolverine` is called) unless told otherwise. In Clean Architecture / DDD solutions handlers live in `*.Application` — add `opts.Discovery.IncludeAssembly(...)` for every assembly the inventory lists as containing handlers, or put `[assembly: Wolverine.Attributes.WolverineModule]` in those assemblies.

### 5. Messages

Remove `IRequest`, `IRequest<T>`, `INotification`, `IBaseRequest`, `IStreamRequest<T>` markers. Messages need no interface. They **must be public** (and so must handlers). Records are fine and idiomatic. `Unit` disappears: a handler that returned `Unit` now returns `void`/`Task`. Details: `references/api-mapping.md`.

### 6. Handlers

MediatR handlers are usually already Wolverine-compatible after removing the interface, because Wolverine discovers **public** classes whose name ends in `Handler` or `Consumer` with a public method named `Handle`/`HandleAsync`/`Consume`/`ConsumeAsync` whose first parameter is the message. `CancellationToken` as a later parameter is supported.

For each handler:
1. Remove `: IRequestHandler<...>` / `: INotificationHandler<...>`.
2. Make sure class and method are `public` and the class name ends in `Handler` (rename or add `[WolverineHandler]` otherwise).
3. Change `Task<Unit>` → `Task`, delete `return Unit.Value;`.
4. **Open generic handlers are not supported by Wolverine.** Create one closed handler per concrete message type (the inventory flags these).

Style B extras (static methods, method injection, cascading, compound handlers) are in `references/api-mapping.md`.

### 7. Call sites

| MediatR | Wolverine |
|---|---|
| inject `IMediator` / `ISender` / `IPublisher` | inject `IMessageBus` |
| `await _mediator.Send(cmd, ct)` (no response) | `await _bus.InvokeAsync(cmd, ct)` (default — see classification below) |
| `var r = await _mediator.Send(query, ct)` | `var r = await _bus.InvokeAsync<TResponse>(query, ct)` |
| `await _mediator.Publish(evt, ct)` | **depends** — read `references/notifications.md` |
| `_mediator.CreateStream(...)` | no mediator equivalent — flag for the user |

Always pass the response type explicitly to `InvokeAsync<T>` when the caller uses the result. `T` must exactly match the handler's return type. Calling the non-generic `InvokeAsync` on a handler that returns an object makes Wolverine treat that object as a **cascading message**, not a return value.

**Classify every `Send` call — don't replace them all mechanically.** Record each in a table (call site → request/response, command, or event → chosen API):

| What the call means | API | Behaviour vs. MediatR |
|---|---|---|
| Request/response, or command whose caller must know it succeeded | `InvokeAsync<T>` / `InvokeAsync` | Same: inline, awaited, exceptions reach the caller. **Default.** |
| Command that may run in the background | `SendAsync` | Changes: runs on a queue, caller doesn't see exceptions; **throws if no handler/route is known** for the message. |
| Actually an event (one-to-many, fine if nobody listens) | `PublishAsync` | Changes: runs on a queue, caller doesn't see exceptions; silently ignored if nobody subscribes. |

Only move a call off `InvokeAsync` when the user agrees, because it changes the API's contract (e.g. a 400/500 that used to surface no longer does). `SendAsync`/`PublishAsync` to local handlers need local queues, so they **don't work with `DurabilityMode.MediatorOnly`** — if any call uses them, don't enable that mode.

### 8. Cross-cutting concerns

Port every `IPipelineBehavior`, `IRequestPreProcessor`, `IRequestPostProcessor`, `IRequestExceptionHandler` and `IRequestExceptionAction` using `references/pipeline-behaviors.md`. Do not leave behaviors unported: deleting MediatR deletes the behavior, so validation, logging, authorization or transactions would silently stop running.

Two decisions come first, and both go in the final report:
1. **Where each behavior belongs.** Not every behavior should become Wolverine middleware. Classify each as Wolverine middleware, a built-in Wolverine add-on, ASP.NET Core middleware, an endpoint filter, domain logic, or an infrastructure concern (decision table in `references/pipeline-behaviors.md` §2).
2. **Where the transaction boundary is,** if a transaction/unit-of-work behavior existed: the HTTP request, the message handler, the database transaction plus Wolverine's outbox, or the domain operation (§6). Decide once for the solution; don't let it fall out of whichever API was easiest to port.

### 9. Tests

Follow `references/testing.md`: replace `Mock<IMediator>`/`Substitute.For<ISender>()` with `IMessageBus` doubles, update `Verify(m => m.Send(...))` assertions, and add at least one integration test per ported behavior proving it still runs (e.g. an invalid command is rejected).

### 10. Verify — do not skip

1. `python3 <skill-dir>/scripts/inventory.py <solution-root> --check` → must exit 0 (no MediatR references and no `Wolverine.Shims.MediatR` usage left).
2. `dotnet build` with no new warnings related to the migration.
3. `dotnet test` → all tests green, including the new behavior tests.
4. Confirm handler discovery for at least one handler per assembly: temporarily add `Console.WriteLine(opts.DescribeHandlerMatch(typeof(XHandler)));` inside `UseWolverine`, or run the app's Wolverine CLI (`dotnet run -- describe` / `dotnet run -- codegen preview` when the host ends with `RunJasperFxCommands(args)`; older versions use `RunOaktonCommands`). Remove the temporary line afterwards.
5. Walk through `references/gotchas.md` and confirm each item is handled.

## Reporting back

Finish with a concise summary for the user: handler style used, counts converted per category (from the inventory), the `Send`/`Publish` classification table, where each behavior went, the transaction-boundary decision, every place where runtime semantics changed (notification delivery, error handling, transaction boundaries), anything that needs a human decision (stream requests, open generics, custom exception handlers), and the verification results. Don't recap every file touched — the diff shows that.
