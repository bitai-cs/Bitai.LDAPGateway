using MediatR;
using Microsoft.Extensions.Logging;

namespace Bitai.LDAPGateway.Application.Common.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull, Models.Result
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        // Correlates every log line below without repeating {RequestName} in each message.
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestName"] = requestName
        });

        //_logger.LogInformation("Handling request {RequestName}: {@Request}", typeof(TRequest).Name, request);
        _logger.LogInformation("Handling request.");

        var response = await next()
            ?? throw new InvalidOperationException($"Handler for '{requestName}' returned null.");

        if (response.IsSuccess)
        {
            //_logger.LogInformation("Request {RequestName} completed successfully: {Result}", typeof(TRequest).Name, response);
            _logger.LogInformation("Request completed successfully.");
        }
        else if (response.Error != null)
        {
            //_logger.LogWarning("Request {RequestName} failed with error: {Result}", typeof(TRequest).Name, response);
            LogFailure(response.Error);
        }

        // It is not necessary.
        //_logger.LogInformation("Handled request {RequestName}", typeof(TRequest).Name);

        return response;
    }



    private void LogFailure(Models.Error error)
    {
        // Top-level error first, then walk the InnerError chain via Flatten().
        // -1 (InnerErr / exceptions) and 5xx are treated as server-side failures;
        // everything else (validation, not found, conflict) is a client-side warning.
        foreach (var (current, depth) in error.Flatten().Select((e, i) => (e, i)))
        {
            var isServerError = current.StatusCode is >= 500 or < 0;
            var level = isServerError ? LogLevel.Error : LogLevel.Warning;
            var prefix = depth == 0 ? "Request failed" : new string(' ', depth * 2) + "Caused by";

            _logger.Log(level,
                "{Prefix} [{ErrorCode}] ({StatusCode}): {ErrorMessage}",
                prefix, current.Code, current.StatusCode, current.Message);

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                _logger.LogDebug("{Prefix} StackTrace: {StackTrace}", prefix, current.StackTrace);
            }
        }
    }
}
