# API mapping: MediatR → Wolverine

## Contents
1. Quick reference table
2. Requests and responses
3. Handlers (conservative, style A)
4. Handlers (idiomatic, style B)
5. Call sites
6. DI registration
7. Marker interfaces and generic constraints
8. Thin controllers → Wolverine.HTTP (optional)

---

## 1. Quick reference table

| MediatR | Wolverine | Notes |
|---|---|---|
| `IRequest<TResponse>` | nothing (plain public class/record) | |
| `IRequest` / `IRequest<Unit>` | nothing | `Unit` disappears |
| `INotification` | nothing | see `notifications.md` |
| `IRequestHandler<TReq, TRes>` | public class `*Handler` with `public Task<TRes> Handle(TReq msg, ...)` | |
| `IRequestHandler<TReq>` | `public Task Handle(TReq msg, ...)` | |
| `INotificationHandler<T>` | `public Task Handle(T msg, ...)` | see `notifications.md` |
| `IMediator`, `ISender`, `IPublisher` | `IMessageBus` (namespace `Wolverine`) | |
| `Send(req)` → `TRes` | `InvokeAsync<TRes>(req)` | T must match handler return type exactly |
| `Send(req)` (no response) | `InvokeAsync(req)` by default; `SendAsync` only if the command may run in the background | classify each call — see SKILL.md step 7 |
| `Send(object)` | `InvokeAsync(object)` / `InvokeAsync<T>(object)` | dynamic dispatch works the same |
| `Publish(notification)` | `InvokeAsync`, `PublishAsync`, or cascading | **semantics differ** — `notifications.md` |
| `IPipelineBehavior<,>` | middleware (`Before`/`After`/`Finally`) or built-in add-on | `pipeline-behaviors.md` |
| `IRequestPreProcessor<T>` | `Before`/`Validate`/`Load` middleware | |
| `IRequestPostProcessor<T,R>` | `After` / `AfterCommit` middleware | |
| `IRequestExceptionHandler`, `IRequestExceptionAction` | error-handling policies / call-site try-catch | no 1:1 equivalent |
| `IStreamRequest<T>`, `CreateStream` | no mediator equivalent | flag to user |
| `services.AddMediatR(cfg => ...)` | `builder.Host.UseWolverine(opts => ...)` | |
| `Unit.Value` | delete | |

---

## 2. Requests and responses

```csharp
// Before
public record GetOrder(Guid Id) : IRequest<OrderDto>;
public record CancelOrder(Guid Id, string Reason) : IRequest;
public class CreateOrder : IRequest<Guid> { public string Sku { get; set; } = ""; }

// After
public record GetOrder(Guid Id);
public record CancelOrder(Guid Id, string Reason);
public class CreateOrder { public string Sku { get; set; } = ""; }
```

Requirements:
- The message type **must be public** (Wolverine generates code that references it). `internal record` messages must become `public`.
- Remove `using MediatR;` once nothing else in the file needs it.

Response type advice:
- DTOs, records, `Result<T>`, primitives, and concrete collections (`List<T>`, arrays) are fine as responses through `InvokeAsync<T>`.
- **Avoid returning tuples** from handlers: Wolverine treats each tuple element as a separate return value (cascading message / side effect). Wrap tuple responses in a record.
- Avoid returning `IEnumerable<object>` / `object` — Wolverine treats those as cascading messages. Typed collections should be checked with a test (see `gotchas.md`).

---

## 3. Handlers — conservative (style A)

```csharp
// Before
internal sealed class GetOrderHandler : IRequestHandler<GetOrder, OrderDto>
{
    private readonly AppDbContext _db;
    public GetOrderHandler(AppDbContext db) => _db = db;

    public async Task<OrderDto> Handle(GetOrder request, CancellationToken ct)
        => await _db.Orders.Where(o => o.Id == request.Id).Select(...).SingleAsync(ct);
}

public class CancelOrderHandler : IRequestHandler<CancelOrder>   // MediatR 12+
{
    public async Task Handle(CancelOrder request, CancellationToken ct) { ... }
}

public class LegacyHandler : IRequestHandler<DoThing, Unit>      // MediatR < 12
{
    public async Task<Unit> Handle(DoThing request, CancellationToken ct) { ...; return Unit.Value; }
}

// After
public sealed class GetOrderHandler                // public; still constructor-injected
{
    private readonly AppDbContext _db;
    public GetOrderHandler(AppDbContext db) => _db = db;

    public async Task<OrderDto> Handle(GetOrder request, CancellationToken ct)
        => await _db.Orders.Where(o => o.Id == request.Id).Select(...).SingleAsync(ct);
}

public class CancelOrderHandler
{
    public async Task Handle(CancelOrder request, CancellationToken ct) { ... }
}

public class LegacyHandler
{
    public async Task Handle(DoThing request, CancellationToken ct) { ... }   // no Unit
}
```

Discovery rules (all must hold, or use `[WolverineHandler]` / `IWolverineHandler`):
- Class is **public**, concrete, has a public constructor, name ends with `Handler` or `Consumer`.
- Method is **public**, named `Handle`, `HandleAsync`, `Handles`, `HandlesAsync`, `Consume`, `ConsumeAsync`, `Consumes`, `ConsumesAsync`.
- First parameter is the message.
- **No open generic handler types.** `class CacheInvalidationHandler<T> : INotificationHandler<EntityChanged<T>>` must become one closed handler per `T` (or a non-generic handler taking a shared base type/interface).
- Static classes need `[WolverineHandler]` if you want to be explicit, but the name-suffix convention works for static classes too in current versions; verify with `DescribeHandlerMatch` if unsure.

Vertical-slice nested pattern (common with MediatR):

```csharp
public static class CreateOrder
{
    public record Command(string Sku) : IRequest<Guid>;
    public class Handler : IRequestHandler<Command, Guid> { ... }
}
```

Removing the interfaces is enough as long as `Handler` and `Command` are public. Nested type names like `CreateOrder.Command` are fine.

---

## 4. Handlers — idiomatic (style B)

Only when the user chose style B. Convert one feature at a time and run its tests after each; every feature is converted before the migration is done.

### 4.1 Static handler + method injection

```csharp
public static class GetOrderHandler
{
    public static Task<OrderDto> Handle(GetOrder query, AppDbContext db, CancellationToken ct)
        => db.Orders.Where(o => o.Id == query.Id).Select(...).SingleAsync(ct);
}
```

Any parameter after the message is resolved from DI, or is one of: `CancellationToken`, `IMessageContext`/`IMessageBus` (scoped to the current message), `Envelope`, `ILogger` (Wolverine supplies `ILogger<MessageType>`), `DateTimeOffset now`.

### 4.2 Cascading instead of injected `IPublisher`

```csharp
// Before
public async Task<Guid> Handle(CreateOrder cmd, CancellationToken ct)
{
    var order = Order.Create(cmd.Sku);
    _db.Orders.Add(order);
    await _db.SaveChangesAsync(ct);
    await _publisher.Publish(new OrderCreated(order.Id), ct);
    return order.Id;
}

```

When the caller needs a response *and* the handler raises an event, keep the response as the return value and publish the event explicitly. (Tuple returns such as `(Guid, OrderCreated)` are interpreted by Wolverine as multiple return values; their response handling is version-sensitive, so don't introduce them during a migration.)

```csharp
public static async Task<Guid> Handle(CreateOrder cmd, AppDbContext db, IMessageContext bus, CancellationToken ct)
{
    var order = Order.Create(cmd.Sku);
    db.Orders.Add(order);
    await db.SaveChangesAsync(ct);
    await bus.PublishAsync(new OrderCreated(order.Id));   // see notifications.md for semantics
    return order.Id;
}
```

For handlers that return nothing to the caller, returning the event (or `OutgoingMessages`, or `IEnumerable<object>`) is the clean cascading form:

```csharp
public static OrderCancelled Handle(CancelOrder cmd, Order order) { order.Cancel(cmd.Reason); return new OrderCancelled(order.Id); }
```

### 4.3 Compound handlers (Load / Validate / Handle)

```csharp
public static class ShipOrderHandler
{
    public static async Task<Order?> LoadAsync(ShipOrder cmd, AppDbContext db, CancellationToken ct)
        => await db.Orders.FindAsync([cmd.OrderId], ct);

    public static HandlerContinuation Validate(ShipOrder cmd, Order? order, ILogger logger)
    {
        if (order is null) { logger.LogWarning("Order {Id} not found", cmd.OrderId); return HandlerContinuation.Stop; }
        return HandlerContinuation.Continue;
    }

    public static OrderShipped Handle(ShipOrder cmd, Order order) { order.Ship(); return new OrderShipped(order.Id); }
}
```

Note: when `Validate` returns `Stop` on a handler invoked with `InvokeAsync<T>`, the caller does not get an exception. If the original MediatR code threw (e.g. `NotFoundException` mapped to 404), **keep throwing** to preserve the API contract.

### 4.4 Transactions

With `WolverineFx.EntityFrameworkCore` and `opts.UseEntityFrameworkCoreTransactions()`, mark handlers `[Transactional]` (or `opts.Policies.AutoApplyTransactions()`), and Wolverine calls `SaveChangesAsync` for you after the handler. Remove explicit `SaveChangesAsync` only when this is in place — otherwise data stops being saved.

---

## 5. Call sites

```csharp
// Before
public class OrdersController(ISender sender) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<OrderDto> Get(Guid id, CancellationToken ct) => await sender.Send(new GetOrder(id), ct);

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelOrder body, CancellationToken ct)
    { await sender.Send(body with { Id = id }, ct); return NoContent(); }
}

// After
public class OrdersController(IMessageBus bus) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<OrderDto> Get(Guid id, CancellationToken ct) => await bus.InvokeAsync<OrderDto>(new GetOrder(id), ct);

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancelOrder body, CancellationToken ct)
    { await bus.InvokeAsync(body with { Id = id }, ct); return NoContent(); }
}
```

Minimal API:

```csharp
app.MapGet("/orders/{id}", (Guid id, IMessageBus bus, CancellationToken ct) => bus.InvokeAsync<OrderDto>(new GetOrder(id), ct));
```

`InvokeAsync` also has an optional `timeout` parameter. Long-running handlers that used to run unbounded under MediatR may need an explicit timeout — check the signature in the installed version.

Rules:
- The type argument must be written explicitly (`InvokeAsync<OrderDto>`). MediatR inferred it from `IRequest<T>`; Wolverine cannot.
- Calling `InvokeAsync` (non-generic) on a handler that returns a value makes the returned value a **cascading message**, not a response. Search for `await _mediator.Send(` lines whose result is discarded but whose handler returns a value — they're fine to convert to non-generic `InvokeAsync` **only** if the returned type has no handler and you accept that Wolverine logs "no routes" for it. Otherwise use `InvokeAsync<T>` and discard.
- Background services / hosted services that injected `IMediator` as a singleton: `IMessageBus` can be resolved from the root provider in current versions, but resolving it per unit of work from a created scope mirrors the MediatR pattern and avoids lifetime surprises.

---

## 6. DI registration

```csharp
// Before
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(ApplicationAssemblyMarker).Assembly);

// After
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(ApplicationAssemblyMarker).Assembly);
    opts.Policies.AddMiddleware<LoggingMiddleware>();          // order = registration order
    opts.UseFluentValidation();                                // discovers + registers validators
});
// Remove AddValidatorsFromAssembly OR use opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration)
```

Clean Architecture note: the `Application` project often had an `AddApplication()` extension that called `AddMediatR`. Either keep the extension for other registrations and move Wolverine config to the host, or have the Application layer expose `public static void ConfigureApplication(this WolverineOptions opts)` that the host calls inside `UseWolverine`. The second keeps the "layer owns its config" style without the Application project needing `WolverineFx` beyond the options type.

---

## 7. Marker interfaces and generic constraints

MediatR's `IRequest<T>` often leaks into app code: `where TRequest : IRequest<Result>`, `ICommand : IRequest<Result>`, `IQuery<T> : IRequest<T>`.

- If the app has its own `ICommand`/`IQuery<T>` that extend MediatR interfaces, **keep the app interfaces** and just remove the MediatR base. They become useful for targeting middleware: `opts.Policies.ForMessagesOfType<ICommand>().AddMiddleware(typeof(UnitOfWorkMiddleware))`.
- Generic constraints that only existed to satisfy MediatR (inside behaviors) go away with the behaviors.

---

## 8. Thin controllers → Wolverine.HTTP (optional, style B)

If a controller action does nothing but `return await _sender.Send(x)`, `WolverineFx.Http` can expose the handler logic directly:

```csharp
public static class GetOrderEndpoint
{
    [WolverineGet("/orders/{id}")]
    public static Task<OrderDto> Get(Guid id, AppDbContext db, CancellationToken ct) => ...;
}
// Program.cs: builder.Services.AddWolverineHttp(); ... app.MapWolverineEndpoints();
```

This changes routing, OpenAPI output, filters and auth attributes, so treat it as a separate, opt-in step after the mediator migration is green — never bundled into it.
