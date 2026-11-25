using JasperFx.Core;
using StackExchange.Redis;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.SqlServer;   // or Wolverine.Postgresql

// Program.cs / Wolverine configuration
var messageStoreConnectionString = builder.Configuration.GetConnectionString("Wolverine")
    ?? throw new InvalidOperationException("Wolverine message store connection string is missing.");

builder.Host.UseWolverine(opts =>
{
    // Make sure handlers in Application AND Infrastructure assemblies are discovered.
    opts.Discovery.IncludeAssembly(typeof(CacheInvalidationRequested).Assembly);
    opts.Discovery.IncludeAssembly(typeof(CacheInvalidationRequestedHandler).Assembly);

    // Transactional outbox (adapt to the persistence in use).
    opts.PersistMessagesWithSqlServer(messageStoreConnectionString);   // or PersistMessagesWithPostgresql
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();
    opts.Policies.UseDurableLocalQueues();                        // durable inbox for every local queue

    // Dedicated queue so invalidations retry without competing with business work.
    // If only this queue should be durable, drop the global policy above and use
    // opts.LocalQueue("cache-invalidation").UseDurableInbox();
    opts.PublishMessage<CacheInvalidationRequested>()
        .ToLocalQueue("cache-invalidation");

    // Retry transient Redis failures.
    opts.OnException<RedisConnectionException>()
        .RetryWithCooldown(50.Milliseconds(), 250.Milliseconds(), 1.Seconds());
    opts.OnException<RedisTimeoutException>()
        .RetryWithCooldown(50.Milliseconds(), 250.Milliseconds(), 1.Seconds());
});
