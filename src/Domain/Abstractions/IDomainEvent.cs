namespace Bitai.LDAPGateway.Domain.Abstractions;

public interface IDomainEvent
{
   DateTime OccurredOnUtc { get; }
}
