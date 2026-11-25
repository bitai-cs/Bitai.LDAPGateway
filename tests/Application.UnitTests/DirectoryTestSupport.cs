using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Domain.Abstractions;
using Bitai.LDAPGateway.Domain.Enums;
using Bitai.LDAPGateway.Domain.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Application.UnitTests;

/// <summary>Shared fakes for the use-case boundary tests: a mocked connector, a recording event publisher and an in-memory cache.</summary>
internal sealed class DirectoryTestSupport
{
   public const string Profile = "EDU";
   public const CatalogType Catalog = CatalogType.GC;

   public DirectoryTestSupport()
   {
      FusionCache = new FusionCache(new FusionCacheOptions { CacheKeyPrefix = "test:" });
      Cache = new DirectoryCache(FusionCache, Invalidator.Object, NullLogger<DirectoryCache>.Instance);

      // The fake invalidator applies the invalidation to the in-memory FusionCache so the tests observe
      // real read-after-write behavior. Individual tests can override this setup to simulate Redis failures.
      Invalidator
         .Setup(i => i.InvalidateAsync(
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>()))
         .Returns<IReadOnlyCollection<string>, IReadOnlyCollection<string>, CancellationToken>(async (keys, tags, _) =>
         {
            foreach (var key in keys)
            {
               await FusionCache.RemoveAsync(key);
            }

            if (tags.Count > 0)
            {
               await FusionCache.RemoveByTagAsync(tags.ToArray());
            }
         });
   }

   public Mock<IDirectoryServiceConnector> Connector { get; } = new();
   public Mock<IDomainEventPublisher> Publisher { get; } = new();
   public Mock<ICacheInvalidator> Invalidator { get; } = new();
   public IFusionCache FusionCache { get; }
   public DirectoryCache Cache { get; }

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

   public void VerifyProfileInvalidated()
   {
      Invalidator.Verify(
         i => i.InvalidateAsync(
            It.IsAny<IReadOnlyCollection<string>>(),
            It.Is<IReadOnlyCollection<string>>(tags => tags.Contains(CacheTags.Profile(Profile))),
            It.IsAny<CancellationToken>()),
         Times.Once);
   }

   public void VerifyProfileNotInvalidated()
   {
      Invalidator.Verify(
         i => i.InvalidateAsync(
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>()),
         Times.Never);
   }

   public static bool IsContext(LdapRequestContext c) => c.ServerProfile == Profile && c.CatalogType == Catalog;
}
