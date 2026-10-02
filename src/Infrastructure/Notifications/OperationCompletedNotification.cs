using Bitai.LDAPGateway.Domain.Events;

namespace Bitai.LDAPGateway.Infrastructure.Notifications;

public sealed record OperationCompletedNotification(OperationCompletedDomainEvent DomainEvent);
