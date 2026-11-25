using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Events;

namespace Bitai.LDAPGateway.Application.Common;

public abstract class LdapHandlerBase
{
    private readonly IDomainEventPublisher _domainEventPublisher;

    protected LdapHandlerBase(IDomainEventPublisher domainEventPublisher)
    {
        _domainEventPublisher = domainEventPublisher;
    }

    protected async Task<Result<T>> ExecuteAsync<T>(
        string operationName,
        LdapRequestContext context,
        Func<Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var result = await operation();

        await PublishCompletedAsync(operationName, context, result.IsSuccess, result, cancellationToken);

        return result;
    }

    /// <summary>Executes an operation that does not target a directory catalog (e.g. configuration reads) and publishes the completion event without one.</summary>
    protected async Task<Result<T>> ExecuteWithoutCatalogAsync<T>(
        string operationName,
        string? serverProfile,
        Func<Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var result = await operation();

        await _domainEventPublisher.PublishAsync(
            new OperationCompletedDomainEvent(
                operationName,
                serverProfile,
                null,
                result.IsSuccess,
                result.IsSuccess ? "Operation completed" : result.Error?.Message ?? "Operation failed",
                DateTime.UtcNow),
            cancellationToken);

        return result;
    }

    protected async Task<Result> ExecuteAsync(
        string operationName,
        LdapRequestContext context,
        Func<Task<Result>> operation,
        CancellationToken cancellationToken)
    {
        var result = await operation();

        await PublishCompletedAsync(operationName, context, result.IsSuccess, result, cancellationToken);

        return result;
    }

    /// <summary>Publishes the operation-completed domain event for the current request.</summary>
    protected Task PublishCompletedAsync<T>(
        string operationName,
        LdapRequestContext context,
        bool isSuccess,
        Result<T> result,
        CancellationToken cancellationToken,
        string? successMessage = null)
    {
        return _domainEventPublisher.PublishAsync(
            new OperationCompletedDomainEvent(
                operationName,
                context.ServerProfile,
                context.CatalogType,
                isSuccess,
                isSuccess ? successMessage ?? "Operation completed" : result.Error?.Message ?? "Operation failed",
                DateTime.UtcNow),
            cancellationToken);
    }

    /// <summary>Publishes the operation-completed domain event for a non-generic result.</summary>
    protected Task PublishCompletedAsync(
        string operationName,
        LdapRequestContext context,
        bool isSuccess,
        Result result,
        CancellationToken cancellationToken,
        string? successMessage = null)
    {
        return _domainEventPublisher.PublishAsync(
            new OperationCompletedDomainEvent(
                operationName,
                context.ServerProfile,
                context.CatalogType,
                isSuccess,
                isSuccess ? successMessage ?? "Operation completed" : result.Error?.Message ?? "Operation failed",
                DateTime.UtcNow),
            cancellationToken);
    }
}
