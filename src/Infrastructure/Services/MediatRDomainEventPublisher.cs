using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Events;
using Bitai.LDAPGateway.Infrastructure.Notifications;
using MediatR;

namespace Bitai.LDAPGateway.Infrastructure.Services;

public sealed class MediatRDomainEventPublisher : IDomainEventPublisher
{
   private readonly IMediator _mediator;

   public MediatRDomainEventPublisher(IMediator mediator)
   {
      _mediator = mediator;
   }

   public Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken)
   {
      return domainEvent switch
      {
         OperationCompletedDomainEvent operationCompleted => _mediator.Publish(new OperationCompletedNotification(operationCompleted), cancellationToken),
         _ => Task.CompletedTask
      };
   }
}
