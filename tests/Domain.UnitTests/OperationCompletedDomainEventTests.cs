using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.Events;

namespace Bitai.LDAPGateway.Domain.UnitTests;

public sealed class OperationCompletedDomainEventTests
{
   [Fact]
   public void Constructor_ValidValues_ShouldStoreAllProperties()
   {
      var occurredOn = DateTime.UtcNow;

      var ev = new OperationCompletedDomainEvent(
         "Authenticate",
         "EDU",
         CatalogType.LC,
         true,
         "Operation completed",
         occurredOn);

      Assert.Equal("Authenticate", ev.OperationName);
      Assert.Equal("EDU", ev.ServerProfile);
      Assert.Equal(CatalogType.LC, ev.CatalogType);
      Assert.True(ev.IsSuccess);
      Assert.Equal("Operation completed", ev.Message);
      Assert.Equal(occurredOn, ev.OccurredOnUtc);
   }
}
