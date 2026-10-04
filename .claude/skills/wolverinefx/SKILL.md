---
name: wolverinefx
description: Rules and verified pitfalls for WolverineFx (Wolverine) in Clean Architecture .NET services. Use whenever the task involves UseWolverine, IMessageBus, InvokeAsync, message handlers (`*Handler` classes, Handle methods), cascading messages, FluentValidation or other Wolverine middleware, EF Core transactions, the durable inbox/outbox, idempotency or deduplication, error policies and retries, Wolverine logging or OpenTelemetry, codegen (`codegen write`, TypeLoadMode.Static), tracked-session tests, or replacing MediatR with Wolverine. Also use when reviewing code that touches any of these, even if the user only says "handler", "command/query" or "outbox".
---
# WolverineFx Technical Reference (Clean Architecture)

Use this skill before writing, changing or reviewing any code that uses Wolverine (WolverineFx). Source of truth: https://wolverinefx.net. When neither this skill nor the codebase settles a decision, ask the user rather than guessing; a wrong guess in architecture or transaction behavior is expensive to undo.

Detailed material lives in `references/` and is loaded only when needed:

| File | Read when |
| --- | --- |
| `references/codegen-production.md` | Preparing for production, `codegen write`, `TypeLoadMode.Static`, startup errors about code generation, the CI gate for generated code |
| `references/logging-and-results.md` | Configuring Wolverine logging, messages that carry secrets, failures returned as `Result` values |
| `references/testing.md` | Writing or reviewing tests: handlers, tracked sessions, transports, host and discovery, persistence and outbox |

Verify NuGet package ids and versions on NuGet before adding references. Target the current Wolverine major version unless the project pins another one, and confirm compatibility with the project's target framework before upgrading. The transports page (https://wolverinefx.net/guide/messaging/transports/) lists RabbitMQ, Azure Service Bus, Amazon SQS and SNS, Google Pub/Sub, Kafka, Pulsar, NATS, MQTT, Redis, SignalR and database-backed transports (SQL Server, PostgreSQL, MySQL, SQLite). ActiveMQ is not among them. Check that page for the broker in use, and if it is not listed, ask the user instead of improvising.

Claims marked "verified" were checked by running code against WolverineFx 6.44.0 (xUnit, a class library with no Wolverine reference hosted by a project that calls `UseWolverine`). Names and behavior can change between versions: re-check them against the version the project uses, especially after an upgrade.

## 1. Wolverine boundary and project profile

The Domain does not reference Wolverine: that keeps business rules independent of the messaging framework. Domain events are plain records.

How much Wolverine the Application layer may use is a design decision with two valid profiles; the Wolverine documentation pages reviewed (handlers, cascading messages, testing) do not prescribe layer rules. Because the choice shapes every handler and cannot be inferred from the code, the user confirms the profile before you write or review code that touches Wolverine in Application. If the `dotnet-clean-arch-specialist-w` agent is in use, follow its confirmation protocol; otherwise ask directly, suggesting pragmatic as the default. Do not switch profiles silently once confirmed.

| Profile | Application may reference | Typical use of it |
| --- | --- | --- |
| Pragmatic (suggested default) | the core `WolverineFx` package only | `IMessageBus` / `IMessageContext`, `OutgoingMessages`, `Envelope`, Wolverine attributes, static handlers |
| Purist | nothing from Wolverine | plain handlers; sending messages goes through an abstraction owned by Application and implemented in Infrastructure |

Under both profiles:

- Application never references transports (`WolverineFx.RabbitMQ`, `WolverineFx.AzureServiceBus`, `WolverineFx.Kafka`, ...), persistence or outbox packages (`WolverineFx.EntityFrameworkCore`, `WolverineFx.Postgresql`, `WolverineFx.SqlServer`, `WolverineFx.Marten`), `WolverineFx.Http`, or Wolverine configuration.
- All Wolverine configuration, middleware, policies and error handling rules live in the composition root (Presentation) and Infrastructure. Prefer Infrastructure or Presentation for middleware classes even where the pragmatic profile would allow them in Application.
- Enforce the confirmed profile with an ArchUnitNET test: Domain never references Wolverine; under purist neither does Application; under pragmatic Application references no Wolverine package other than the core one.

### What works with zero Wolverine references (verified)

Checked in a class library with no Wolverine reference, hosted by a project that calls `UseWolverine` with `IncludeAssembly` for that library:

- Public classes whose name ends in `Handler` are discovered, instance or static, without `[WolverineHandler]`.
- A handler can return a plain tuple such as `(OrderReply, OrderPlaced)`: `InvokeAsync<OrderReply>` receives the response and `OrderPlaced` cascades to its own handler. Per the Wolverine docs, a single plain message, `object`, `IEnumerable<object>` and tuples all cascade without Wolverine types.
- Wolverine types are needed only for `OutgoingMessages`, `Envelope`, `ISendMyself`, `Respond.*` (per the docs), `IMessageBus` / `IMessageContext`, and attributes.

So the purist profile loses less than it seems: the Wolverine-typed helpers, bus access inside handlers, and attributes (use policies configured from the composition root instead). It keeps convention discovery, static handlers, plain cascading, and middleware and policies configured from the composition root.

### Layer responsibilities (DDD / Clean Architecture)

| Layer | Responsibility | Wolverine role |
| --- | --- | --- |
| Domain | Entities, Value Objects, Aggregates and Aggregate Roots, Domain Events, repository interfaces, domain services | None. Pure C#. Aggregates raise domain events into an encapsulated collection exposed read-only. Event types implement a plain `IDomainEvent` marker defined in the Domain. |
| Application | Use cases: commands, queries, DTOs, validators, handlers, Application abstractions | Purist: none. Pragmatic: the core `WolverineFx` package only (section 1). Either way, plain handlers discovered by convention (section 2). |
| Infrastructure | EF Core, repositories, brokers and transports, outbox persistence | All Wolverine configuration: `UseWolverine`, transports (RabbitMQ, Azure Service Bus, Kafka), EF Core integration, transactional outbox, middleware, policies, error handling. |
| Presentation / API | HTTP endpoints and other external interfaces | Thin adapters that call `IMessageBus.InvokeAsync()`. MVC controllers, Minimal APIs or Wolverine.HTTP (section 7). |

Dependencies point inward: Presentation and Infrastructure depend on Application; Application depends on Domain; Domain depends on nothing.

### Modules, bounded contexts and event sourcing

- If the solution is a modular monolith, each module (bounded context) has its own Domain, Application and Infrastructure. Modules communicate only through integration events and messages over Wolverine, never through references to another module's Domain or Application. Whether the solution is a modular monolith or a single application is a decision for the user: ask when the codebase does not show it.
- Event-driven does not require event sourcing. The default persistence here is EF Core. Marten, event streams and projections are only introduced when the user asks for them and event sourcing gives real business value. Ask before introducing them.

## 2. Handlers and discovery

Wolverine discovers handlers by convention:

- Public, concrete classes whose name ends with `Handler` (e.g. `CreateUserCommandHandler`); the Wolverine docs also list the `Consumer` suffix. Instance handlers with constructor injection of Application abstractions are the default. Static handler classes and static methods are supported and were verified to be discovered by name without `[WolverineHandler]`; use them when the codebase convention or a pure-function handler benefits, and use `[WolverineHandler]` (pragmatic profile only) when the naming does not fit.
- Public instance methods named `Handle` / `HandleAsync`. The first parameter is the message.
- Constructor injection and method injection are both allowed. Prefer constructor injection of Application abstractions.
- Messages and handlers need no marker interface; the docs offer `IWolverineHandler` or `[WolverineHandler]` only as an explicit alternative to naming conventions. Open generic handlers are not supported by Wolverine (stated in the discovery docs).
- Discovery scans an allow list of assemblies, by default only the host (application) assembly. Handlers live in the Application assembly, so add it in the composition root:
  `opts.Discovery.IncludeAssembly(typeof(IApplicationAssemblyMarker).Assembly);`
- If a handler is not found, diagnose with `opts.DescribeHandlerMatch(typeof(X))`.
- Callers dispatch with `IMessageBus.InvokeAsync<TResponse>(message, ct)` from Presentation or Infrastructure; from Application only under the pragmatic profile, and under purist through an abstraction owned by Application (e.g. `IMessageDispatcher`) implemented in Infrastructure.
- Each handler coordinates only: business logic, persistence through repository abstractions, domain events or messages to emit, and the return value. Validation, authorization, logging, transactions and retries are middleware and policies, not handler code.
- When a handler must both return a response to the caller and emit messages, a plain tuple `(Response, Message)` was verified (`InvokeAsync<Response>` returns the response and the message cascades). For any other shape, do not guess: read the Wolverine return-values docs or ask the user.

### `InvokeAsync` pitfalls (verified)

- A message with no handler or route: `InvokeAsync` throws `IndeterminateRoutesException`.
- Non-generic `InvokeAsync(message)` on a handler that returns a value gives nothing to the caller: the returned value is routed as a cascading message (the log shows `No routes can be determined for Envelope ... (<ReturnType>)` when nobody handles it). Use `InvokeAsync<TResponse>` whenever the caller needs the result. A `Result`-style return value counts as a value.
- The type argument must be compatible with the handler's declared return type. A base type or interface works (`InvokeAsync<IReadOnlyList<int>>` and `InvokeAsync<object>` on a handler returning `List<int>`). An unrelated type, such as `InvokeAsync<string>`, returns `null` silently with no exception. Keep `TResponse` equal to the declared return type and do not hide a `null` behind `!`.

### Handlers and the domain model

- Business rules and invariants live in the Domain (aggregates, value objects, domain services), never in handlers, middleware or repositories. A handler that is mostly `if` logic over entity state is an anemic-domain smell.
- A command handler follows one shape: load the aggregate through its repository interface, call behavior on the aggregate root, stage the change through the repository, return the result. The transactional middleware commits.
- Repositories are per aggregate root, not per table. Changes inside one aggregate are one transaction. Do not modify several aggregates in one handler: coordinate them with domain events and eventual consistency.
- Query handlers return DTOs or projections and do not load aggregates for read-only screens.
- Name commands after business intent (`PlaceOrder`, `CancelOrder`, `ApprovePayment`), not generic state changes (`UpdateOrderStatus`, `SetOrderState`). Name domain events as facts in the past tense (`OrderPlaced`, `PaymentCompleted`), never as commands (`PlaceOrderEvent`).
- Domain events describe something that happened inside the domain (past tense, e.g. `UserCreated`). Handlers for them are ordinary handlers in Application. Events that cross a bounded-context or service boundary are integration events: define them separately (e.g. `OrderPlacedIntegrationEvent`), map a domain event to an integration event explicitly in Application or Infrastructure, and publish it through the outbox. Do not turn every internal domain event into an external integration event, and do not expose the domain model to external consumers.

## 3. Validation (FluentValidation)

Use the `WolverineFx.FluentValidation` middleware. Validators run BEFORE the handler; on failure Wolverine throws `FluentValidation.ValidationException` and the handler does not execute.

- Validators live in Application, are `public`, and are named `CreateUserCommandValidator`.
- Register validators exactly once. `opts.UseFluentValidation()` scans and registers validators by default, so do not also register them another way. For a layered solution prefer `opts.UseFluentValidation(RegistrationBehavior.ExplicitRegistration)` and register validators once in `AddApplication()`, as Singleton when stateless.
- Avoid validators with IoC dependencies; they can force service location in generated code. Do infrastructure-dependent checks (e.g. uniqueness) in the handler through an Application abstraction.
- The middleware is applied only to message types that have registered validators.
- Wolverine.HTTP: use the HTTP FluentValidation ProblemDetails middleware (`UseFluentValidationProblemDetailMiddleware()` inside `MapWolverineEndpoints`).
- With `InvokeAsync()` the `ValidationException` propagates to the caller and is mapped to 400 by the centralized exception handler. For messages received from a queue or broker, `opts.UseFluentValidation()` also registers an error handling policy that discards a message when `ValidationException` is thrown: invalid messages are not retried and do not reach an error queue.

## 4. Middleware

Cross-cutting concerns are Wolverine middleware, policies and built-in features. Never hand-write a generic pipeline wrapper around handlers. Middleware is code woven into the generated handler pipeline.

Conventional middleware lives in Infrastructure/Presentation and MAY reference Wolverine:

- Method names (case sensitive): `Before`/`BeforeAsync`/`Load`/`LoadAsync`/`Validate`/`ValidateAsync` (before the handler), `After`/`AfterAsync` (after the handler, BEFORE the transaction commit), `AfterCommit`/`AfterCommitAsync` (after a durable commit), `Finally`/`FinallyAsync` (finally block).
- The first parameter is the message or an interface it implements; other parameters are injected.
- A before method can return `HandlerContinuation.Stop` to stop processing.
- Apply selectively: `opts.Policies.ForMessagesOfType<IAuthorizedRequest>().AddMiddleware(typeof(AuthorizationMiddleware));`. Do not use reflection or optional-service checks inside middleware to decide whether it applies.
- Marker interfaces that target middleware (e.g. `IAuthorizedRequest`) are plain interfaces defined in Application.

Concern to Wolverine mechanism:

| Concern | Wolverine mechanism |
| --- | --- |
| Validation | `WolverineFx.FluentValidation` middleware, section 3 |
| Logging | Built-in logging (ILogger of the message type), section 9 |
| Performance metrics | Built-in OpenTelemetry traces and metrics, section 10 |
| Exceptions | Error handling policies plus centralized HTTP exception handling, section 8 |
| Authorization | Conventional middleware on `IAuthorizedRequest`; `[Authorize]` / `RequireAuthorizeOnAll()` on Wolverine.HTTP endpoints |
| Transactions | EF Core transactional middleware plus `AutoApplyTransactions()`, section 5 |
| Idempotency | Durable inbox and opt-in logical deduplication, section 6 |

### Authorization middleware

Authorize every command and query. Depend on an Application abstraction for the decision and throw `ForbiddenException` (mapped to 403) when denied.

```csharp
public sealed class AuthorizationMiddleware
{
    public async Task BeforeAsync(
        IAuthorizedRequest request,
        IRequestAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        if (!await authorizer.IsAuthorizedAsync(request, cancellationToken))
            throw new ForbiddenException();
    }
}
```

Favor policy-based authorization behind `IRequestAuthorizer`. On the HTTP edge use `[Authorize]` and policies on controllers; on Wolverine.HTTP endpoints use `[Authorize]` and `opts.RequireAuthorizeOnAll()` (overridden by `[AllowAnonymous]`).

## 5. Transactions and outbox

The EF Core transactional middleware is the Unit of Work. Do not write a custom transaction wrapper.

```csharp
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(IApplicationAssemblyMarker).Assembly);

    // Message storage for inbox/outbox (SQL Server shown; other RDBMS stores exist)
    opts.PersistMessagesWithSqlServer(connectionString, "wolverine");

    // EF Core as the transactional middleware / outbox provider
    opts.UseEntityFrameworkCoreTransactions();

    // Opt in: wrap every handler that depends on a DbContext
    opts.Policies.AutoApplyTransactions();

    // Local queues become durable (inbox/outbox)
    opts.Policies.UseDurableLocalQueues();

    opts.Services.AddDbContextWithWolverineIntegration<AppDbContext>(x =>
        x.UseSqlServer(connectionString));
});
```

Rules:

- Wolverine detects a transactional handler when a `DbContext` is a method argument, a dependency of an injected service, or a dependency of a constructor-injected service. A repository (Infrastructure) taking the `DbContext` in its constructor therefore enrolls the handler, which stays free of EF Core.
- If Application depends on an abstraction implemented directly by the `DbContext` (e.g. `IUnitOfWork`), register it: `opts.UseEntityFrameworkCoreTransactions().WithDbContextAbstraction<IUnitOfWork, AppDbContext>();` and register the abstraction with a factory forwarding to the SAME scoped `DbContext` (not `AddScoped<TAbs, TImpl>()`).
- If a handler depends on more than one `DbContext`, Wolverine will not guess and fails at startup. Ask the user which context owns the transaction, then designate it in configuration.
- Repositories must NOT call `SaveChangesAsync()` under the transactional middleware. The middleware commits.
- Default mode is `Eager` (explicit transaction before the handler). `TransactionMiddlewareMode.Lightweight` relies on `SaveChangesAsync()` only and cannot roll back work already committed. `Eager` conflicts with `EnableRetryOnFailure()`; Wolverine fails at startup on that combination. Set-based operations (`ExecuteUpdateAsync`, etc.) require `Eager`. Decide consciously and tell the user the mode and why.
- Outbox: messages cascaded from the handler or published through Wolverine are persisted by the same `SaveChangesAsync()` as the entities and relayed after commit. Make outgoing endpoints durable (`UseDurableOutbox()` per endpoint, or `opts.Policies.UseDurableOutboxOnAllSendingEndpoints()`) and incoming listeners durable (`UseDurableInbox()` or `opts.Policies.UseDurableInboxOnAllListeners()`).
- `After` middleware runs before the commit. Use `AfterCommit` for effects that must follow a durable write.

### Domain events

The Domain keeps its own marker interface and aggregate supertype with no Wolverine reference. The list is private and exposed read-only:

```csharp
public interface IDomainEvent;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _events = [];

    public IReadOnlyCollection<IDomainEvent> Events => _events;

    protected void Raise(IDomainEvent domainEvent) => _events.Add(domainEvent);
}
```

The aggregate decides whether an operation is valid and raises the event as part of that behavior:

```csharp
public void Place()
{
    EnsureCanBePlaced();
    Status = OrderStatus.Placed;
    Raise(new OrderPlaced(Id));
}
```

Wolverine then scrapes the events inside the transaction (requires the EF Core transactional middleware). This registration is NOT verified with the read-only accessor above; the docs only show the documented form `public List<object> Events { get; } = new();`:

```csharp
opts.PublishDomainEventsFromEntityFrameworkCore<AggregateRoot>(x => x.Events);
```

- The docs (https://wolverinefx.net/guide/durability/efcore/domain-events.html) show three overloads: `<TEntity>(x => x.Events)`, `<TEntity>()` (a scoped `OutgoingDomainEvents` buffer with `IEventPublisher`) and a non-generic one. Whether a read-only `IReadOnlyCollection<IDomainEvent>` accessor compiles and scrapes correctly against the version in use is not confirmed: test it before relying on it, and if it does not work, tell the user and agree on an alternative (for example the `<TEntity>()` buffer overload) instead of silently changing the Domain.
- The docs do not state whether Wolverine clears the collection after publishing. Ask the user before adding a clearing method to the aggregate.
- Scraping depends completely on the EF Core transactional middleware. Before Wolverine 6.41 it ran only on the `Eager` path and silently did nothing for `Lightweight` handlers. Check the version in use.
- The domain does not know who consumes its events.

Domain event handlers are ordinary Wolverine handlers.

## 6. Idempotency

Be precise about the guarantee:

- The durable inbox on a LISTENING endpoint (broker or durable local queue) gives no-more-than-once handling per message id: duplicates already known to the inbox are discarded before the handler. The handled record is kept for `opts.Durability.KeepAfterMessageHandling` (default 5 minutes) to absorb broker redelivery.
- `InvokeAsync()` calls are NOT deduplicated by this mechanism.
- Logical deduplication (business identity, e.g. the same invoice number) is opt-in, needs an RDBMS message store, applies to fire-and-forget handling (not to replaying a stored response), and needs `opts.Durability.EnableMessageDeduplication = true;` plus a `DeduplicationWindow` sized to how late a duplicate can plausibly arrive (default 24 h).
- `[Deduplicated]` is a Wolverine attribute: allowed in Application only under the pragmatic profile. Under purist, or to keep Application free of it, enforce it from the composition root: `opts.Policies.RequireDeduplicationId(chain => chain.MessageType.CanBeCastTo<ICreateCommand>());` and derive the id with `opts.MessageDeduplication.ByMessage<T>(x => ...)`. Deriving an id only stamps it; enforcement is a separate step on the consuming side.
- Never claim exactly-once processing. Handlers with side effects outside the database transaction must still be idempotent by design.

## 7. Host setup and Presentation

Wolverine is configured with `UseWolverine()` on the host builder (not `IServiceCollection`). Expose it as a host extension such as `UseWolverineMessaging()` in Infrastructure, called from Program.cs, to keep Program.cs small.

Controllers and endpoints stay thin. Both styles are allowed. Follow the codebase convention; if unclear, ask the user. Do not mix styles inside one feature.

MVC controller or Minimal API:

```csharp
[HttpPost]
public async Task<ActionResult<UserDto>> Create(CreateUserCommand command, CancellationToken ct)
{
    var user = await _bus.InvokeAsync<UserDto>(command, ct);
    return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
}
```

Wolverine.HTTP (`WolverineFx.Http`, `app.MapWolverineEndpoints()`):

```csharp
public static class CreateUserEndpoint
{
    [WolverinePost("/api/users")]
    public static Task<UserDto> Post(CreateUserCommand command, IMessageBus bus, CancellationToken ct)
        => bus.InvokeAsync<UserDto>(command, ct);
}
```

Before using Wolverine.HTTP's own mediator-style or inline-logic options, read the Wolverine.HTTP docs. In this architecture endpoint methods remain thin adapters and business logic stays in Application handlers.

### Code generation and production

Wolverine generates the handler pipeline code at runtime by default. For production use Static mode with the generated code committed, and make `WolverineFx.RuntimeCompilation` Debug-only. Read `references/codegen-production.md` before touching `codegen write`, `TypeLoadMode`, `Program.cs` startup or the CI gate for generated code.

## 8. Error handling

Two separate concerns:

1. HTTP mapping: centralized ASP.NET Core `IExceptionHandler` plus ProblemDetails. `InvokeAsync()` rethrows handler exceptions to the caller, so mapping happens at the HTTP edge: ValidationException 400, UnauthorizedAccessException 401, ForbiddenException 403, NotFoundException 404, ConflictException 409, unexpected 500. Never expose stack traces, SQL errors or internal exceptions.
2. Resilience: Wolverine error handling policies for transient failures:

```csharp
opts.Policies.OnException<SqlException>()
    .RetryWithCooldown(50.Milliseconds(), 100.Milliseconds(), 250.Milliseconds());

opts.Policies.OnException<InvalidMessageException>().Discard();

opts.Policies.OnException<TimeoutException>().ScheduleRetry(5.Seconds());
```

Limits:

- With `IMessageBus.InvokeAsync()` only Retry and Retry-With-Cooldown apply automatically. Requeue, ScheduleRetry, MoveToErrorQueue, Discard and pause actions apply to asynchronous (queued or broker) handling. Do not rely on `Requeue()` to protect an HTTP request path.
- Filter on specific SQL error codes. Do not retry every `SqlException`.
- Per-listener circuit breakers exist for broker endpoints.
- Dead-lettered messages are stored by Wolverine on durable stores and can be inspected and replayed.

## 9. Logging

Wolverine already logs message execution through `ILogger<TMessage>` (the category is the message type), so do not write generic logging middleware. Configure levels and tracing in the composition root (`MessageExecutionLogLevel`, `MessageSuccessLogLevel`, `InvokeTracing = InvokeTracingMode.Full` when using `InvokeAsync`). Wolverine logs `message.ToString()` when a handler fails, so a message that carries a secret must mask it. Failures returned as `Result` values are treated as success by Wolverine. Read `references/logging-and-results.md` for the configuration list, the masking pattern and the Result logging policy.

## 10. Observability

Wolverine emits OpenTelemetry traces and metrics natively. Do not write a stopwatch or performance middleware.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Wolverine"))
    .WithMetrics(m => m.AddMeter("Wolverine*"));
```

The meter is named `Wolverine:{ApplicationName}`, so the wildcard is required; a bare `AddMeter("Wolverine")` matches nothing. Built-in metrics include messages sent, succeeded, execution time, execution failures, dead-letter count and inbox/outbox/scheduled depths. Handler-level diagnostics are opt-in under `opts.Tracking`. Watch for personal data in telemetry tags.

## 11. Testing

Tests mirror the layers: Domain and Application unit tests need no Wolverine host; integration and host tests use tracked sessions (`Wolverine.Tracking`) and never sleeps. Read `references/testing.md` for handler unit tests, tracked sessions, transport isolation, host and discovery tests, persistence and outbox tests, and the list of what a Wolverine solution must always test.

## 12. Wolverine review checklist

- The Wolverine profile (pragmatic or purist) was confirmed by the user; Domain has no Wolverine reference and Application follows the confirmed profile.
- Handlers are public, end with `Handler` (static handlers only where the codebase uses them), and the Application assembly is included in discovery.
- Business rules live in aggregates and value objects; handlers only orchestrate.
- One aggregate is modified per handler and repositories are per aggregate root.
- Domain events are raised by aggregates, named as past-tense facts; cross-boundary integration events are separate types mapped explicitly.
- Commands are named after business intent.
- No event sourcing or Marten unless the user asked for it.
- Validators are public and registered exactly once.
- Repositories do not call `SaveChangesAsync()` under transactional middleware.
- Transaction mode (`Eager` vs `Lightweight`) is a conscious choice and compatible with `EnableRetryOnFailure()`.
- Handlers with more than one `DbContext` designate the transactional one.
- Outgoing and incoming endpoints are durable where the outbox/inbox guarantee is claimed.
- Idempotency claims are accurate (inbox vs `InvokeAsync` vs logical deduplication); side effects outside the transaction are idempotent.
- Error policies are not relied on for `InvokeAsync` beyond Retry and Retry-With-Cooldown.
- `InvokeTracing`, OpenTelemetry source and the `Wolverine*` meter are configured.
- Audited members and telemetry tags contain no personal data or secrets.
- Production runs `TypeLoadMode.Static` with the generated code committed, `WolverineFx.RuntimeCompilation` is Debug-only, and CI fails when regenerating changes `Internal/Generated`.
- Messages that carry secrets mask them in `ToString()` (Wolverine logs failed messages) and a test proves it.
- If failures are returned as `Result` values, they are logged by a policy-attached method and the code does not rely on `After(Result)`.
- `InvokeAsync<TResponse>` is used wherever the caller needs the result, with `TResponse` equal to the handler's declared return type.
- Handlers are unit tested without a host; integration tests wait with tracked sessions and never with sleeps or `Task.Delay`.
- Tests assert that the host discovers a handler for every command and query, and no test host uses `DurabilityMode.MediatorOnly` together with tracked sessions.
