using Bitai.LDAPGateway.Domain.Events;
using MediatR;

namespace Bitai.LDAPGateway.Infrastructure.Notifications;

public sealed record OperationCompletedNotification(OperationCompletedDomainEvent DomainEvent) : INotification;
