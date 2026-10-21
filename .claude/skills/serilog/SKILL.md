---
name: serilog
description: >
  Structured logging with Serilog for .NET 10 applications. Covers two-stage
  bootstrap, appsettings configuration, enrichers, sinks, request logging,
  destructuring, and Serilog.Expressions.
  Load this skill when setting up Serilog, configuring log sinks, enrichers,
  or structured logging, or when the user mentions "Serilog", "structured
  logging", "log enrichment", "Seq", "LogContext", "UseSerilog",
  "WriteTo", "message template", "Serilog.Expressions", "request logging",
  "log sink", "rolling file", or "audit log".
---

# Serilog

## Core Principles

1. **Two-stage initialization** — Create a bootstrap logger for startup, then replace it with the full logger after DI is ready. This captures startup errors that would otherwise be lost.
2. **`AddSerilog()` over `UseSerilog()`** — Use `builder.Services.AddSerilog()` (the modern API) instead of `builder.Host.UseSerilog()`. It integrates with DI services via `ReadFrom.Services(services)`.
3. **Message templates, not interpolation** — `{PropertyName}` syntax creates structured data that can be queried. String interpolation (`$"..."`) breaks structure and allocates even when the log level is disabled.
4. **Configure via appsettings.json** — Keep log levels, sinks, and overrides in configuration so they can change per environment without redeployment.

## Patterns

### Two-Stage Bootstrap Setup

```csharp
using Serilog;

// Stage 1: Bootstrap logger — captures startup errors before DI
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting application");

    var builder = WebApplication.CreateBuilder(args);

    // Stage 2: Full logger with DI and configuration
    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "MyApp.Api"));

    var app = builder.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent",
                httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
```

### appsettings.json Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600
        }
      },
      {
        "Name": "Seq",
        "Args": { "serverUrl": "http://localhost:5341" }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName"],
    "Destructure": [
      { "Name": "ToMaximumDepth", "Args": { "maximumDestructuringDepth": 4 } },
      { "Name": "ToMaximumStringLength", "Args": { "maximumStringLength": 1024 } },
      { "Name": "ToMaximumCollectionCount", "Args": { "maximumCollectionCount": 10 } }
    ]
  }
}
```

Override section uses namespace prefixes matched against `SourceContext`. More specific prefixes take precedence.

### Request Logging Middleware

Replaces the multiple per-request log events from ASP.NET Core with a single summary event.

```csharp
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    options.GetLevel = (httpContext, elapsed, ex) => ex is not null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : LogEventLevel.Information;

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId",
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
    };
});
```

### Structured Logging and Destructuring

```csharp
// Named properties — creates queryable structured data
logger.LogInformation("Order {OrderId} placed by {CustomerId} for {Total:C}",
    orderId, customerId, total);

// @ operator preserves object structure as properties
logger.LogInformation("Processing {@SensorInput}", sensorInput);
// Output: Processing {"Latitude": 25, "Longitude": 134}

// $ operator forces ToString()
logger.LogInformation("Received {$Data}", new[] { 1, 2, 3 });
// Output: Received "System.Int32[]"
```

### Scoped Properties with LogContext

```csharp
using (LogContext.PushProperty("CorrelationId", correlationId))
using (LogContext.PushProperty("TenantId", tenantId))
{
    logger.LogInformation("Processing order {OrderId}", orderId);
    // CorrelationId and TenantId attached to ALL log events in this scope
}
```

Requires `.Enrich.FromLogContext()` on the logger configuration.

### Correlation and Trace Context

Propagate a trusted correlation ID at the HTTP boundary so every event generated
while handling a request can be joined across services. Preserve an existing
header when it is valid; otherwise generate a new value. Also record the W3C
trace ID when tracing is active.

```csharp
using System.Diagnostics;
using Serilog.Context;

app.Use(async (httpContext, next) =>
{
    var correlationId = httpContext.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(correlationId) ||
        correlationId.Length > 128 ||
        correlationId.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_'))
    {
        correlationId = Guid.NewGuid().ToString("N");
    }

    httpContext.Response.Headers["X-Correlation-ID"] = correlationId;

    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty(
        "TraceId", Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier))
    {
        await next();
    }
});
```

Place this before request logging and application middleware. Forward
`X-Correlation-ID` on outgoing service calls, but validate or replace
untrusted values to avoid log injection and unbounded property sizes.

### Asynchronous Sinks and Hot Paths

Use a sink's native batching when available. For slow, non-audit sinks that do
not provide it, `Serilog.Sinks.Async` can isolate application request threads
from sink I/O:

```csharp
using Serilog.Debugging;

SelfLog.Enable(Console.Error);

.WriteTo.Async(
    sink => sink.File("logs/app-.log", rollingInterval: RollingInterval.Day),
    bufferSize: 10_000,
    blockWhenFull: false)
```

`blockWhenFull: false` preserves application responsiveness but can drop events
when the buffer is exhausted; monitor `SelfLog` and choose a buffer size based
on the acceptable loss window. `blockWhenFull: true` avoids drops but can
increase request latency during a sink outage. Do not wrap `AuditTo` sinks:
audit events are intentionally synchronous and failures must remain visible.

In hot paths, avoid expensive value construction, broad object destructuring,
and per-item information logs. Prefer aggregate events, appropriate log levels,
and `[LoggerMessage]` methods for frequently executed messages.

### OpenTelemetry Sink (OTLP Export)

Export Serilog events directly to any OTLP backend without the OpenTelemetry SDK:

```csharp
.WriteTo.OpenTelemetry(options =>
{
    options.Endpoint = "http://localhost:4317";
    options.Protocol = OtlpProtocol.Grpc;
    options.ResourceAttributes = new Dictionary<string, object>
    {
        ["service.name"] = "MyApp.Api",
        ["deployment.environment"] = "production"
    };
})
```

### Serilog.Expressions for Filtering

Requires the `Serilog.Expressions` package.

```csharp
// Exclude health check noise
.Filter.ByExcluding("RequestPath like '/health%'")

// Route errors to a separate file
.WriteTo.Conditional("@l = 'Error'",
    wt => wt.File("logs/errors-.log", rollingInterval: RollingInterval.Day))
```

### [LoggerMessage] Source Generator for Hot Paths

Built into `Microsoft.Extensions.Logging.Abstractions` — compile-time generated, zero allocations when the level is disabled.

```csharp
public static partial class OrderLogs
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Order {OrderId} created for {CustomerId}")]
    public static partial void OrderCreated(this ILogger logger, Guid orderId, Guid customerId);
}

// Usage
logger.OrderCreated(order.Id, order.CustomerId);
```

## Anti-patterns

### Don't Use String Interpolation

```csharp
// BAD — breaks structured logging, allocates even when level is disabled
logger.LogInformation($"Order {orderId} created for {customerId}");

// GOOD — message template with named parameters
logger.LogInformation("Order {OrderId} created for {CustomerId}", orderId, customerId);
```

### Don't Skip CloseAndFlush

```csharp
// BAD — async sinks (Seq, OTLP, Elasticsearch) lose buffered events
app.Run();

// GOOD — wrap in try/finally
try { app.Run(); }
catch (Exception ex) { Log.Fatal(ex, "Unhandled exception"); }
finally { await Log.CloseAndFlushAsync(); }
```

### Don't Log Sensitive Data

```csharp
// BAD — passwords and tokens in logs
logger.LogInformation("Login: {Email} with password {Password}", email, password);

// GOOD — never log secrets, passwords, tokens, or PII
logger.LogInformation("Login: {Email}", email);
```

### Don't Log Exceptions as Properties

```csharp
// BAD — the exception is an ordinary property; the stack trace may be absent.
logger.LogError("Could not process order {OrderId}: {Exception}", orderId, ex);

// GOOD — pass the exception as the first argument.
logger.LogError(ex, "Could not process order {OrderId}", orderId);
```

### Don't Let Non-Audit Sink Failures Go Unobserved

Async buffers and networked sinks can fill or fail while the application
continues running. Enable and monitor Serilog `SelfLog`, decide explicitly
whether event loss or caller blocking is acceptable, and test the selected
behavior under sink outage.

### Don't Destructure Without Limits

```csharp
// BAD — large object graphs cause memory issues and massive log entries
logger.LogInformation("Request: {@Request}", httpContext.Request);

// GOOD — configure destructuring limits
.Destructure.ToMaximumDepth(4)
.Destructure.ToMaximumStringLength(1024)
.Destructure.ToMaximumCollectionCount(10)

// BETTER — destructure to specific properties
.Destructure.ByTransforming<HttpRequest>(r => new { r.Method, r.Path })
```

### Don't Use the Deprecated Elasticsearch Sink

```csharp
// BAD — the Serilog.Sinks.Elasticsearch PACKAGE is deprecated
// <PackageReference Include="Serilog.Sinks.Elasticsearch" />
.WriteTo.Elasticsearch("http://localhost:9200")

// GOOD — same method name, but from the official Elastic.Serilog.Sinks
// package, which writes ECS-formatted documents to data streams
// <PackageReference Include="Elastic.Serilog.Sinks" />
.WriteTo.Elasticsearch([new Uri("https://elastic.example.com:9200")], opts =>
    opts.DataStream = new DataStreamName("logs", "myapp"))
```

### Minimize Sensitive Identifiers

Do not log raw secrets, tokens, passwords, or PII. When operations need to be
correlated by a sensitive identifier, log a keyed fingerprint instead of the
source value; store the key in a secret store and rotate it according to the
organization's policy.

```csharp
using System.Security.Cryptography;
using System.Text;

static string Fingerprint(string value, byte[] key) =>
    Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));

logger.LogInformation("Login failed for {UserFingerprint}",
    Fingerprint(email, fingerprintKey));
```

For audit events that must be written before the caller continues, use
`AuditTo`. Unlike ordinary sinks, audit sink failures propagate to the caller:

```csharp
.AuditTo.File("logs/audit-.log", rollingInterval: RollingInterval.Day)
```

## Decision Guide

| Scenario | Recommendation |
|----------|---------------|
| Application logging | Serilog with `AddSerilog()` and appsettings.json |
| Log storage (development) | Seq (free single-user) or Aspire Dashboard |
| Log storage (production) | Seq, Elasticsearch (Elastic sink), or OTLP backend |
| Request logging | `UseSerilogRequestLogging()` (replaces per-request noise) |
| Scoped properties | `LogContext.PushProperty()` in middleware |
| Cross-service correlation | Validate or create a correlation ID; enrich it with the active trace ID |
| Log filtering | `Serilog.Expressions` for expression-based filtering |
| High-performance paths | `[LoggerMessage]` source generator |
| Audit trails | `AuditTo` (synchronous, exceptions propagate) |
| Slow or remote non-audit sink | Prefer native batching; otherwise use a bounded async wrapper and monitor `SelfLog` |
| Sensitive identifier correlation | Log a keyed HMAC fingerprint, never the raw identifier |
| Log levels by environment | `MinimumLevel.Override` per namespace in appsettings |
| OpenTelemetry integration | `Serilog.Sinks.OpenTelemetry` (no SDK dependency) |
