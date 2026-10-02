using Wolverine;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Events;
using Bitai.LDAPGateway.Infrastructure.Notifications;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class WolverineDomainEventPublisher : IDomainEventPublisher
{
   private readonly IMessageBus _bus;

   public WolverineDomainEventPublisher(IMessageBus bus)
   {
      _bus = bus;
   }

   public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
   {
      return domainEvent switch
      {
         OperationCompletedDomainEvent operationCompleted => _bus.InvokeAsync(new OperationCompletedNotification(operationCompleted), cancellationToken),
         _ => Task.CompletedTask
      };
   }
}
