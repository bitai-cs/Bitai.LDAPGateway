using Microsoft.Extensions.Logging;
using Wolverine;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Events;
using Bitai.LDAPGateway.Infrastructure.Notifications;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class WolverineDomainEventPublisher : IDomainEventPublisher
{
   private readonly IMessageBus _bus;
   private readonly ILogger<WolverineDomainEventPublisher> _logger;

   public WolverineDomainEventPublisher(IMessageBus bus, ILogger<WolverineDomainEventPublisher> logger)
   {
      _bus = bus;
      _logger = logger;
   }

   public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
   {
      return domainEvent switch
      {
         OperationCompletedDomainEvent operationCompleted => _bus.InvokeAsync(new OperationCompletedNotification(operationCompleted), cancellationToken),
         _ => PublishUnknown(domainEvent)
      };
   }

   private Task PublishUnknown(IDomainEvent domainEvent)
   {
      _logger.LogWarning("Domain event of type {DomainEventType} has no publisher mapping and was not published.", domainEvent.GetType().FullName);
      return Task.CompletedTask;
   }
}
