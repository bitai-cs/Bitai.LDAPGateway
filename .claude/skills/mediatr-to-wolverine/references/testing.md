# Updating tests

## 1. Mocks of IMediator / ISender / IPublisher

`IMessageBus.InvokeAsync` has optional parameters (`CancellationToken`, and a `TimeSpan? timeout` in current versions). Moq/NSubstitute expressions must match **all** parameters, including optional ones, or the setup silently never matches. Check the exact signature in the installed version (F12 / decompile) before writing setups.

### Moq

```csharp
// Before
var mediator = new Mock<ISender>();
mediator.Setup(m => m.Send(It.IsAny<GetOrder>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new OrderDto(...));
...
mediator.Verify(m => m.Send(It.Is<CancelOrder>(c => c.Id == id), It.IsAny<CancellationToken>()), Times.Once);

// After
var bus = new Mock<IMessageBus>();
bus.Setup(b => b.InvokeAsync<OrderDto>(It.IsAny<GetOrder>(), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()))
   .ReturnsAsync(new OrderDto(...));
...
bus.Verify(b => b.InvokeAsync(It.Is<CancelOrder>(c => c.Id == id), It.IsAny<CancellationToken>(), It.IsAny<TimeSpan?>()), Times.Once);
```

Note the first parameter of `InvokeAsync` is `object`, so `It.IsAny<GetOrder>()` matches by runtime type check inside Moq — fine, but `It.IsAny<object>()` would match every message.

### NSubstitute

```csharp
var bus = Substitute.For<IMessageBus>();
bus.InvokeAsync<OrderDto>(Arg.Any<GetOrder>(), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>())
   .Returns(new OrderDto(...));
await bus.Received(1).InvokeAsync(Arg.Is<CancelOrder>(c => c.Id == id), Arg.Any<CancellationToken>(), Arg.Any<TimeSpan?>());
```

### Wolverine's test double

Wolverine ships a `TestMessageContext` (implements `IMessageContext`/`IMessageBus`) that records sent, published and invoked messages and can be told what an `InvokeAsync<T>` should return. Its namespace and API have moved between versions — search the installed assemblies for `class TestMessageContext` and prefer it over hand-written mocks when it exists, because it's resilient to signature changes.

## 2. Handler unit tests

Conservative (style A) handlers keep their constructors, so tests that `new` the handler and call `Handle(request, ct)` keep working after removing `Unit` assertions:

```csharp
// Before
var result = await handler.Handle(new CancelOrder(id, "x"), CancellationToken.None);
result.Should().Be(Unit.Value);
// After
await handler.Handle(new CancelOrder(id, "x"), CancellationToken.None);
```

Static (style B) handlers are called directly with their dependencies as arguments — simpler than constructor setup. Pure handlers that return cascading messages are asserted on the return value instead of verifying a mocked `IPublisher`.

## 3. Integration tests

- Bootstrap through `WebApplicationFactory<Program>` (or Alba). If a test host is built some other way, set `opts.ApplicationAssembly = typeof(Program).Assembly` — otherwise handler discovery can pick the test assembly. In test runs with several hosts, the application assembly is cached process-wide; symptoms are order-dependent "No routes can be determined" failures.
- Override Wolverine settings for tests with `services.RunWolverineInSoloMode()` / `services.DisableAllExternalWolverineTransports()` when the app later adds brokers.
- For anything asynchronous (`PublishAsync`, cascading, local queues), wait for completion with Wolverine's tracking helpers (`Wolverine.Tracking`):

```csharp
var session = await host.InvokeMessageAndWaitAsync(new PlaceOrder(...));
session.Sent.SingleMessage<OrderPlaced>().OrderId.Should().Be(...);
// or: await host.TrackActivity().Timeout(10.Seconds()).InvokeMessageAndWaitAsync(...)
```

Without this, tests that passed under MediatR's inline `Publish` become flaky after moving to `PublishAsync`.

## 4. Behavior regression tests (add these)

One per ported behavior, at the integration level (through `IMessageBus` or HTTP):

| Behavior | Test |
|---|---|
| Validation | invalid command → expected exception / 400 ProblemDetails, handler not executed |
| Authorization | unauthorized user → expected exception / 403 |
| Transaction | handler throws after writes → nothing persisted |
| Logging/timing | log sink captures expected entry (only if format matters) |
| Notification inline semantics | failing notification handler → caller sees exception (option 1) |

## 5. Architecture tests

If the solution uses NetArchTest/ArchUnitNET rules like "handlers must implement `IRequestHandler`" or "Application must depend on MediatR", rewrite them as: handler classes are public and end with `Handler`; no assembly references `MediatR`. These rules are a cheap guard against regressions after the migration.
