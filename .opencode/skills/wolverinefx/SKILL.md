---
name: wolverinefx
description: Technical reference for WolverineFx in Clean Architecture .NET services: handlers, middleware, validation, EF Core transactions and outbox, idempotency, error policies, logging, OpenTelemetry, and testing.
---
# WolverineFx Technical Reference (Clean Architecture)

Use this skill before writing, changing or reviewing any code that uses Wolverine (WolverineFx). Source of truth: https://wolverinefx.net. Do not guess: when something here or in the codebase does not settle a decision, ask the user.

Verify NuGet package ids and versions on NuGet before adding references. Target the current Wolverine major version unless the project pins another one, and confirm compatibility with the project's target framework before upgrading. ActiveMQ has no Wolverine transport in the documentation reviewed; RabbitMQ, Azure Service Bus and Kafka do. For any other broker or database, ask the user instead of improvising.

## 1. Wolverine boundary rule

The Application layer is plain C#. It MUST NOT reference any Wolverine package or type:

- no `IMessageBus`, `IMessageContext`, `Envelope`, `HandlerContinuation`, `OutgoingMessages`
- no Wolverine attributes (`[WolverineHandler]`, `[Transactional]`, `[Deduplicated]`, `[Middleware]`, `[WolverineLogging]`, `[Audit]`, ...)
- no Wolverine base classes or marker interfaces

All Wolverine configuration, middleware, policies and error handling rules live in the composition root (Presentation) and Infrastructure. Enforce with an ArchUnitNET test that fails if Application or Domain references Wolverine.

### Layer responsibilities (DDD / Clean Architecture)

| Layer | Responsibility | Wolverine role |
| --- | --- | --- |
| Domain | Entities, Value Objects, Aggregates and Aggregate Roots, Domain Events, repository interfaces, domain services | None. Pure C#. Aggregates raise domain events into an encapsulated collection exposed read-only. Event types implement a plain `IDomainEvent` marker defined in the Domain. |
| Application | Use cases: commands, queries, DTOs, validators, handlers, Application abstractions | None. Plain handlers discovered by convention (section 2). No Wolverine types, attributes or static handlers. |
| Infrastructure | EF Core, repositories, brokers and transports, outbox persistence | All Wolverine configuration: `UseWolverine`, transports (RabbitMQ, Azure Service Bus, Kafka), EF Core integration, transactional outbox, middleware, policies, error handling. |
| Presentation / API | HTTP endpoints and other external interfaces | Thin adapters that call `IMessageBus.InvokeAsync()`. MVC controllers, Minimal APIs or Wolverine.HTTP (section 7). |

Dependencies point inward: Presentation and Infrastructure depend on Application; Application depends on Domain; Domain depends on nothing.

### Modules, bounded contexts and event sourcing

- If the solution is a modular monolith, each module (bounded context) has its own Domain, Application and Infrastructure. Modules communicate only through integration events and messages over Wolverine, never through references to another module's Domain or Application. Whether the solution is a modular monolith or a single application is a decision for the user: ask when the codebase does not show it.
- Event-driven does not require event sourcing. The default persistence here is EF Core. Marten, event streams and projections are only introduced when the user asks for them and event sourcing gives real business value. Ask before introducing them.

## 2. Handlers and discovery

Wolverine discovers handlers by convention:

- Public, concrete, NON-static classes whose name ends with `Handler` (e.g. `CreateUserCommandHandler`). Static handler classes need `[WolverineHandler]`, which would put a Wolverine reference in Application, so do not use static handlers.
- Public instance methods named `Handle` / `HandleAsync`. The first parameter is the message.
- Constructor injection and method injection are both allowed. Prefer constructor injection of Application abstractions.
- No marker interfaces on messages or handlers. No open generic handlers.
- Discovery scans an allow list of assemblies, by default only the host (application) assembly. Handlers live in the Application assembly, so add it in the composition root:
  `opts.Discovery.IncludeAssembly(typeof(IApplicationAssemblyMarker).Assembly);`
- If a handler is not found, diagnose with `opts.DescribeHandlerMatch(typeof(X))`.
- Callers dispatch with `IMessageBus.InvokeAsync<TResponse>(message, ct)`, only from Presentation or Infrastructure.
- Each handler coordinates only: business logic, persistence through repository abstractions, domain events or messages to emit, and the return value. Validation, authorization, logging, transactions and retries are middleware and policies, not handler code.
- When a handler must both return a response to the caller and emit messages, do not guess a signature. Read the Wolverine return-values docs or ask the user.

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
- With `InvokeAsync()` the `ValidationException` propagates to the caller and is mapped to 400 by the centralized exception handler. For broker-received messages, Wolverine's registration discards them on `ValidationException`.

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

Wolverine then scrapes the events inside the transaction (requires the EF Core transactional middleware):

```csharp
opts.PublishDomainEventsFromEntityFrameworkCore<AggregateRoot>(x => x.Events);
```

- The Wolverine docs show the accessor as `List<object> Events` and a parameterless `PublishDomainEventsFromEntityFrameworkCore()` overload. Whether a read-only `IReadOnlyCollection<IDomainEvent>` accessor compiles against the version in use is not confirmed by the docs reviewed: check it, and ask the user if it does not.
- The docs reviewed do not state whether Wolverine clears the collection after publishing. Ask the user before adding a clearing method to the aggregate.
- Scraping depends completely on the EF Core transactional middleware. Before Wolverine 6.41 it ran only on the `Eager` path and silently did nothing for `Lightweight` handlers. Check the version in use.
- The domain does not know who consumes its events.

Domain event handlers are ordinary Wolverine handlers.

## 6. Idempotency

Be precise about the guarantee:

- The durable inbox on a LISTENING endpoint (broker or durable local queue) gives no-more-than-once handling per message id: duplicates already known to the inbox are discarded before the handler. The handled record is kept for `opts.Durability.KeepAfterMessageHandling` (default 5 minutes) to absorb broker redelivery.
- `InvokeAsync()` calls are NOT deduplicated by this mechanism.
- Logical deduplication (business identity, e.g. the same invoice number) is opt-in, needs an RDBMS message store, applies to fire-and-forget handling (not to replaying a stored response), and needs `opts.Durability.EnableMessageDeduplication = true;` plus a `DeduplicationWindow` sized to how late a duplicate can plausibly arrive (default 24 h).
- `[Deduplicated]` is a Wolverine attribute, so in this architecture enforce it from the composition root: `opts.Policies.RequireDeduplicationId(chain => chain.MessageType.CanBeCastTo<ICreateCommand>());` and derive the id with `opts.MessageDeduplication.ByMessage<T>(x => ...)`. Deriving an id only stamps it; enforcement is a separate step on the consuming side.
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

Wolverine already logs message execution through `ILogger<TMessage>`; the category is the MESSAGE type, not the handler type, so filter log levels by message type. Do not write logging middleware. Never log passwords, tokens, secrets, connection strings or personal data unless explicitly required. Configure in the composition root:

- `opts.Policies.MessageExecutionLogLevel(LogLevel)` and `opts.Policies.MessageSuccessLogLevel(LogLevel)`
- `opts.Policies.LogMessageStarting(LogLevel)` to log the start of each execution
- When used as an in-process mediator through `InvokeAsync()`, set `opts.InvokeTracing = InvokeTracingMode.Full;` otherwise inline invocations do not emit the same structured logs as transport-received messages.
- Business context: `opts.Policies.ForMessagesOfType<IAccountMessage>().Audit(x => x.AccountId)` (direct member access only). Audited members go to logs and telemetry: never audit personal data or secrets.
- Per-message overrides without Wolverine attributes in Application: use the policy API, not `[WolverineLogging]`.

## 10. Observability

Wolverine emits OpenTelemetry traces and metrics natively. Do not write a stopwatch or performance middleware.

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource("Wolverine"))
    .WithMetrics(m => m.AddMeter("Wolverine*"));
```

The meter is named `Wolverine:{ApplicationName}`, so the wildcard is required; a bare `AddMeter("Wolverine")` matches nothing. Built-in metrics include messages sent, succeeded, execution time, execution failures, dead-letter count and inbox/outbox/scheduled depths. Handler-level diagnostics are opt-in under `opts.Tracking`. Watch for personal data in telemetry tags.

## 11. Testing

- Handlers are plain classes: unit test them directly with fakes for Application abstractions; no Wolverine host needed.
- Integration tests: bootstrap the real application with `WebApplicationFactory` or Alba so Wolverine resolves the correct application assembly and handler discovery. In test processes that start several Wolverine hosts, set `opts.ApplicationAssembly` explicitly or include the handler assembly, because discovery can otherwise silently differ between hosts. Use tracked sessions (`host.TrackActivity()`) to await asynchronous handling and cascading messages.
- Integration tests that touch persistence or the outbox use xUnit with Testcontainers and a real database engine matching production, not an in-memory substitute.
- Architecture tests must verify: Application and Domain do not reference Wolverine; Domain does not reference Application, Infrastructure or Presentation; handlers are public, non-static and end with `Handler`.

## 12. Wolverine review checklist

- Domain and Application contain no Wolverine reference.
- Handlers are public, non-static, end with `Handler`, and the Application assembly is included in discovery.
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
