using Bitai.LDAPGateway.Application.Common.Models;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Bitai.LDAPGateway.Application.Common.Behaviors;

/// <summary>
/// Wolverine middleware replacing the former MediatR LoggingBehavior. Before/Finally are registered with
/// opts.Policies.AddMiddleware; LogResult is attached by ResultLoggingPolicy (an After(Result) parameter
/// is NOT bound by Wolverine, see references/pipeline-behaviors.md section 5).
/// </summary>
public static class ResultLoggingMiddleware
{
    public static IDisposable? Before(ILogger logger, Envelope envelope)
    {
        // Correlates every log line below without repeating {RequestName} in each message.
        var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestName"] = envelope.Message?.GetType().Name ?? envelope.MessageType ?? "unknown"
        });

        logger.LogInformation("Handling request.");
        return scope;
    }

    // Not named After on purpose: Wolverine would treat it as a lifecycle method and fail codegen.
    public static void LogResult(Result result, ILogger logger)
    {
        if (result.IsSuccess)
        {
            logger.LogInformation("Request completed successfully.");
        }
        else if (result.Error != null)
        {
            LogFailure(logger, result.Error);
        }
    }

    public static void Finally(IDisposable? scope) => scope?.Dispose();

    private static void LogFailure(ILogger logger, Error error)
    {
        // Top-level error first, then walk the InnerError chain via Flatten().
        // -1 (InnerErr / exceptions) and 5xx are treated as server-side failures;
        // everything else (validation, not found, conflict) is a client-side warning.
        foreach (var (current, depth) in error.Flatten().Select((e, i) => (e, i)))
        {
            var isServerError = current.StatusCode is >= 500 or < 0;
            var level = isServerError ? LogLevel.Error : LogLevel.Warning;
            var prefix = depth == 0 ? "Request failed" : new string(' ', depth * 2) + "Caused by";

            logger.Log(level,
                "{Prefix} [{ErrorCode}] ({StatusCode}): {ErrorMessage}",
                prefix, current.Code, current.StatusCode, current.Message);

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                logger.LogDebug("{Prefix} StackTrace: {StackTrace}", prefix, current.StackTrace);
            }
        }
    }
}
