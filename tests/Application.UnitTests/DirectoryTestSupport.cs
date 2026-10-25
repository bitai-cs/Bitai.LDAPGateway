using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.Events;
using Moq;

namespace Bitai.LDAPGateway.Application.UnitTests;

internal sealed class DirectoryTestSupport
{
   public const string Profile = "EDU";
   public const CatalogType Catalog = CatalogType.GC;

   public Mock<IDirectoryServiceConnector> Connector { get; } = new();
   public Mock<IDomainEventPublisher> Publisher { get; } = new();

   public static LdapEntryDto Entry(string samAccountName = "jdoe") => new() { samAccountName = samAccountName };

   public static Error Failure => new("bad_gateway", "directory unavailable", 502);

   public void VerifyEvent(string operation, bool isSuccess)
   {
      Publisher.Verify(
         p => p.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()),
         Times.Once);

      Publisher.Verify(
         p => p.PublishAsync(
            It.Is<IDomainEvent>(e => e is OperationCompletedDomainEvent
               && ((OperationCompletedDomainEvent)e).OperationName == operation
               && ((OperationCompletedDomainEvent)e).ServerProfile == Profile
               && ((OperationCompletedDomainEvent)e).CatalogType == Catalog
               && ((OperationCompletedDomainEvent)e).IsSuccess == isSuccess),
            It.IsAny<CancellationToken>()),
         Times.Once);
   }

   public static bool IsContext(LdapRequestContext context) =>
      context.ServerProfile == Profile && context.CatalogType == Catalog;
}
