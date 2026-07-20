using MediatR;
using Microsoft.Extensions.Logging;

namespace Bitai.LDAPGateway.Infrastructure.Notifications;

public sealed class OperationCompletedNotificationHandler : INotificationHandler<OperationCompletedNotification>
{
   private readonly ILogger<OperationCompletedNotificationHandler> _logger;

   public OperationCompletedNotificationHandler(ILogger<OperationCompletedNotificationHandler> logger)
   {
      _logger = logger;
   }

   public Task Handle(OperationCompletedNotification notification, CancellationToken cancellationToken)
   {
      var ev = notification.DomainEvent;
      _logger.LogInformation(
         "LDAP operation {OperationName} completed. Profile={Profile} Catalog={Catalog} Success={Success} Message={Message}",
         ev.OperationName,
         ev.ServerProfile,
         ev.CatalogType,
         ev.IsSuccess,
         ev.Message);

      return Task.CompletedTask;
   }
}
