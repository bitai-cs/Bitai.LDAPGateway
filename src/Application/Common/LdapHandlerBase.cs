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

      await _domainEventPublisher.PublishAsync(
         new OperationCompletedDomainEvent(
            operationName,
            context.ServerProfile,
            context.CatalogType,
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

      await _domainEventPublisher.PublishAsync(
         new OperationCompletedDomainEvent(
            operationName,
            context.ServerProfile,
            context.CatalogType,
            result.IsSuccess,
            result.IsSuccess ? "Operation completed" : result.Error?.Message ?? "Operation failed",
            DateTime.UtcNow),
         cancellationToken);

      return result;
   }
}
