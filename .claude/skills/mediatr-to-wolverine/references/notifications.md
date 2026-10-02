# Notifications: INotification / INotificationHandler / Publish

Read this whole file before converting any `Publish` call. This is where a mechanical migration most often changes runtime behavior.

## What MediatR actually does

`await mediator.Publish(notification, ct)` with the default publisher:
- runs **every** `INotificationHandler<T>` **inline**, one after another, in the caller's DI scope (same `DbContext` instance),
- the caller **awaits** all of them,
- the first exception **propagates** to the caller (remaining handlers don't run),
- zero handlers is a silent no-op,
- handlers registered for a base type/interface also receive derived notifications (contravariance),
- nothing is persisted: if the process dies, the work is lost ("fire and forget" in the delivery-guarantee sense even though it's awaited).

Some apps replace the publisher (`TaskWhenAllPublisher`, custom parallel publishers) — check the `AddMediatR` config (`cfg.NotificationPublisher`/`NotificationPublisherType`).

## The three Wolverine options

| | Option 1: `InvokeAsync(evt)` | Option 2: `PublishAsync(evt)` | Option 3: cascading return value |
|---|---|---|---|
| Runs | inline, awaited | in background on a local queue | after the originating handler finishes, via routing (local queue by default) |
| Caller sees handler exceptions | yes | no | no (unless handled inline) |
| DI scope | new scope for the invoked chain | new scope per message | new scope per message |
| Delivery guarantee | none (same as MediatR) | none with buffered queues; **durable** with message persistence + durable local queues | same as option 2 |
| Works with `DurabilityMode.MediatorOnly` | yes | **no** (local queues disabled) | **no** for local routing |
| Best for | behavior-preserving migration | side effects that shouldn't block/fail the request | idiomatic Wolverine (style B) |

### Option 1 — closest to MediatR (default for style A)

```csharp
await _mediator.Publish(new OrderPlaced(order.Id), ct);
// →
await _bus.InvokeAsync(new OrderPlaced(order.Id), ct);
```

With the default `MultipleHandlerBehavior` (classic, combined), Wolverine merges all handlers for `OrderPlaced` into one logical handler and runs them sequentially. Exceptions propagate. Differences you must still check:

- **Zero handlers.** Invoking a message with no handler is an error in Wolverine, whereas MediatR's Publish was a no-op. For every converted `Publish`, confirm at least one handler exists (the inventory lists notification types and their handlers). If a notification can legitimately have zero handlers (e.g. a domain event published "just in case"), either delete the call, use `PublishAsync`, or keep a no-op handler with a comment.
- **DI scope.** MediatR handlers shared the caller's scope. If a notification handler relied on seeing un-saved changes in the same `DbContext`, or on the caller's `SaveChangesAsync` to persist its changes, that coupling breaks. Search notification handlers for `DbContext` usage without their own `SaveChangesAsync`.
- **Order.** MediatR order = DI registration order (usually type-scan order); Wolverine order = discovery order. If any handler depends on another having run first, make it explicit (one handler calls the next step, or cascade).
- **Do not** set `opts.MultipleHandlerBehavior = MultipleHandlerBehavior.Separated` when relying on this option — it splits handlers into separate per-handler subscriptions intended for queued delivery.

### Option 2 — asynchronous

```csharp
await _bus.PublishAsync(new OrderPlaced(order.Id));
```

The request no longer waits for or fails because of the handlers. Use this only when the user agrees the side effects are allowed to be eventual. For guaranteed delivery, also configure message persistence (e.g. `opts.PersistMessagesWithSqlServer(cs)` / PostgreSQL) and `opts.Policies.UseDurableLocalQueues()`, plus the EF Core outbox integration if the event must commit atomically with business data — this is the main *upgrade* Wolverine offers over MediatR, and worth mentioning to the user even if not done now. Integration tests must wait for background work (see `testing.md`).

### Option 3 — cascading (style B)

Return the event from the handler instead of injecting a publisher:

```csharp
public static OrderPlaced Handle(PlaceOrder cmd, AppDbContext db) { ...; return new OrderPlaced(order.Id); }
```

Cascaded messages are published after the handler (and after the transaction commits when transactional middleware is on). Same caveats as option 2.

## Domain events dispatched from `SaveChanges`

A very common Clean Architecture pattern: entities collect `IDomainEvent : INotification`, and a `DbContext.SaveChangesAsync` override or EF interceptor calls `mediator.Publish` for each.

- Conservative: inject `IMessageBus` into the dispatcher and use option 1 (`InvokeAsync`) to preserve inline semantics — mind the zero-handler rule; many domain events have no handler. A safe dispatcher pattern is to look up whether the event type has a handler before invoking, or switch those to `PublishAsync`.
- Avoid injecting `IMessageBus` directly into the `DbContext` constructor if the DbContext is also injected into handlers (circular scope surprises); use an interceptor or a dispatcher service.
- Idiomatic: Wolverine's EF Core integration has dedicated domain-event support (see https://wolverinefx.net/guide/durability/efcore/domain-events) that publishes collected events through the outbox. It is an improvement, not a requirement for removing MediatR; offer it in the final report unless the user asked for it.

## Polymorphic handlers

`INotificationHandler<INotification>` or handlers for a base event type receive every derived notification in MediatR. Wolverine can bind handlers to interfaces/abstract types, but routing rules differ; for each such handler, write a test that publishes a concrete derived event and asserts the base handler ran.

## Checklist per notification type

- [ ] Option chosen (1/2/3) and recorded in the final report
- [ ] At least one handler exists, or the zero-handler case is handled
- [ ] Handler no longer depends on the caller's DI scope / unsaved changes
- [ ] Ordering dependencies made explicit
- [ ] Test covers it
