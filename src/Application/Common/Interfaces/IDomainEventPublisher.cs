using Bitai.LDAPGateway.Domain.Abstractions;

namespace Bitai.LDAPGateway.Application.Common.Interfaces;

public interface IDomainEventPublisher
{
   Task PublishAsync(IDomainEvent domainEvent, CancellationToken cancellationToken);
}
