# Logging, secrets in failure logs and Result failures

Read when: configuring Wolverine logging, handling messages that carry secrets, or logging failures returned as `Result` values.

Wolverine already logs message execution through `ILogger<TMessage>`; the category is the MESSAGE type, not the handler type, so filter log levels by message type. Do not write generic logging middleware; the one exception is failures returned as `Result` values (below). Never log passwords, tokens, secrets, connection strings or personal data unless explicitly required, and remember that Wolverine itself logs a failed message (below). Configure in the composition root:

- `opts.Policies.MessageExecutionLogLevel(LogLevel)` and `opts.Policies.MessageSuccessLogLevel(LogLevel)`
- `opts.Policies.LogMessageStarting(LogLevel)` to log the start of each execution
- When used as an in-process mediator through `InvokeAsync()`, set `opts.InvokeTracing = InvokeTracingMode.Full;` otherwise inline invocations do not emit the same structured logs as transport-received messages.
- Business context: `opts.Policies.ForMessagesOfType<IAccountMessage>().Audit(x => x.AccountId)` (direct member access only). Audited members go to logs and telemetry: never audit personal data or secrets.
- Per-message overrides: use the policy API from the composition root; `[WolverineLogging]` on a message is allowed only under the pragmatic profile.

### Secrets in failure logs (verified on WolverineFx 6.44.0)

When a handler or validator throws, Wolverine logs `Invocation of <message.ToString()> failed!` at Error level. A message record that carries a secret as a plain member writes it in clear text: `Invocation of Register { User = alice, Password = S3cr3t-LeakCheck! } failed!`. This also happens when the secret comes from an inherited DTO. Masking needs no Wolverine reference, so it works under both profiles:

```csharp
public sealed record SetPasswordCommand(string User, string NewPassword)
{
    private bool PrintMembers(StringBuilder builder)       // record ToString uses it
    {
        builder.Append($"User = {User}, NewPassword = ***");
        return true;
    }
};

// A record deriving from a non-sealed record DTO that has the secret: override the virtual one.
public sealed record CreateUserCommand(string Profile) : CreateUserDto
{
    protected override bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"Profile = {Profile}, UserName = {UserName}, Password = ***");
        return true;
    }
};
```

Search every message and the DTOs it inherits for `Password`, `Secret`, `Token`, `ApiKey` and `Credential` members, and add a test that a failing command does not write the value to the logs.

### Failures returned as `Result` values (only if the codebase uses a Result type)

A handler that returns a failed `Result` does not throw, so Wolverine treats it as a success: by default `InvokeAsync` logs nothing for it, and with `opts.InvokeTracing = InvokeTracingMode.Full` the log says `Successfully processed message ...` for a failed result (verified). If failures must be visible, log them from a middleware that reads the return value.

An `After(Result result, ...)` parameter does NOT bind: Wolverine matches middleware parameters by exact type, so a handler returning `Result<T>` fails code generation with `UnResolvableVariableException ... unable to resolve a variable of type ...Result`. What works (verified for `Result` and `Result<T>` handlers) is an `IHandlerPolicy` that feeds the handler's own return variable to a method that is not named `After`/`Before`/`Finally`:

```csharp
public sealed class ResultLoggingPolicy : IHandlerPolicy
{
    public void Apply(IReadOnlyList<HandlerChain> chains, GenerationRules rules, IServiceContainer container)
    {
        foreach (var chain in chains.Where(c => c.MessageType.Namespace!.StartsWith("MyApp.Application")))
        {
            var rv = chain.Handlers.Last().ReturnVariable;
            if (rv is null || !typeof(Result).IsAssignableFrom(rv.VariableType)) continue;

            var call = new MethodCall(typeof(ResultLoggingMiddleware), nameof(ResultLoggingMiddleware.LogResult));
            call.Arguments[0] = rv;                    // Result<T> passed to a Result parameter
            chain.Postprocessors.Add(call);
        }
    }
}
// opts.Policies.Add<ResultLoggingPolicy>();
// usings: JasperFx, JasperFx.CodeGeneration, JasperFx.CodeGeneration.Frames, Wolverine.Configuration, Wolverine.Runtime.Handlers
```

This uses Wolverine types, so under the purist profile (and by preference under pragmatic) both classes live in Infrastructure or the composition root and reference Application's `Result`. Restrict the policy by namespace so handlers that return `Task` are not touched. Check the namespaces on other Wolverine versions.
