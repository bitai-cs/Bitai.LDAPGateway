using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Enums;

namespace Bitai.LDAPGateway.Domain.Events;

public sealed record OperationCompletedDomainEvent(
   string OperationName,
   string ServerProfile,
   CatalogType CatalogType,
   bool IsSuccess,
   string Message,
   DateTime OccurredOnUtc) : IDomainEvent;
