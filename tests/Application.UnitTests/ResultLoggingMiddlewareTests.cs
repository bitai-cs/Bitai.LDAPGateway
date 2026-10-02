using Bitai.LDAPGateway.Application.Common.Behaviors;
using Bitai.LDAPGateway.Application.Common.Models;
using Microsoft.Extensions.Logging;

namespace Bitai.LDAPGateway.Application.UnitTests;

/// <summary>Protects the behavior ported from the former MediatR LoggingBehavior: levels and inner-error chain.</summary>
public sealed class ResultLoggingMiddlewareTests
{
    [Fact]
    public void LogResult_Success_LogsInformation()
    {
        var logger = new FakeLogger();

        ResultLoggingMiddleware.LogResult(Result.Success(), logger);

        Assert.Single(logger.Entries);
        Assert.Equal((LogLevel.Information, "Request completed successfully."), logger.Entries[0]);
    }

    [Fact]
    public void LogResult_ClientError_LogsWarning()
    {
        var logger = new FakeLogger();

        ResultLoggingMiddleware.LogResult(Result.Failure(Error.NotFound("missing")), logger);

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Contains("Request failed [not_found] (404): missing", logger.Entries[0].Message);
    }

    [Fact]
    public void LogResult_ServerErrorWithInnerChain_LogsEachLevelWithCorrectSeverity()
    {
        var logger = new FakeLogger();
        var error = Error.BadGateway("upstream failed", Error.InnerErr("socket closed"));

        ResultLoggingMiddleware.LogResult(Result<string>.Failure(error), logger);

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(LogLevel.Error, logger.Entries[0].Level);
        Assert.Contains("Request failed [bad_gateway] (502): upstream failed", logger.Entries[0].Message);
        Assert.Equal(LogLevel.Error, logger.Entries[1].Level);
        Assert.Contains("  Caused by [inner_error] (-1): socket closed", logger.Entries[1].Message);
    }

    private sealed class FakeLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
