using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.Events;
using Moq;

namespace Bitai.LDAPGateway.Application.UnitTests;

/// <summary>Shared fakes for the use-case boundary tests: a mocked connector and a recording event publisher.</summary>
internal sealed class DirectoryTestSupport
{
   public const string Profile = "EDU";
   public const CatalogType Catalog = CatalogType.GC;

   public Mock<IDirectoryServiceConnector> Connector { get; } = new();
   public Mock<IDomainEventPublisher> Publisher { get; } = new();

   public static LdapEntryDto Entry(string samAccountName = "jdoe") => new() { samAccountName = samAccountName };

   public static IReadOnlyList<LdapEntryDto> Entries(params string[] names) => names.Select(n => Entry(n)).ToList();

   public static Error Failure => new("bad_gateway", "directory unavailable", 502);

   public void VerifyEvent(string operation, bool isSuccess)
   {
      Publisher.Verify(
         p => p.PublishAsync(
            It.Is<IDomainEvent>(e => e is OperationCompletedDomainEvent
               && ((OperationCompletedDomainEvent)e).OperationName == operation
               && ((OperationCompletedDomainEvent)e).IsSuccess == isSuccess
               && ((OperationCompletedDomainEvent)e).ServerProfile == Profile
               && ((OperationCompletedDomainEvent)e).CatalogType == Catalog),
            It.IsAny<CancellationToken>()),
         Times.Once);
   }

   public static bool IsContext(LdapRequestContext c) => c.ServerProfile == Profile && c.CatalogType == Catalog;
}
