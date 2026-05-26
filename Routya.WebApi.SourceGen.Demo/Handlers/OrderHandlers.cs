using Routya.Core.Abstractions;

namespace Routya.WebApi.SourceGen.Demo.Handlers;

public class EmailNotificationHandler(ILogger<EmailNotificationHandler> logger)
    : INotificationHandler<OrderShippedNotification>
{
    public Task Handle(OrderShippedNotification notification, CancellationToken ct = default)
    {
        logger.LogInformation("Email sent: order {OrderId} shipped to customer {CustomerId}",
            notification.OrderId, notification.CustomerId);
        return Task.CompletedTask;
    }
}

public class WarehouseNotificationHandler(ILogger<WarehouseNotificationHandler> logger)
    : INotificationHandler<OrderShippedNotification>
{
    public Task Handle(OrderShippedNotification notification, CancellationToken ct = default)
    {
        logger.LogInformation("Warehouse notified: order {OrderId} marked as dispatched",
            notification.OrderId);
        return Task.CompletedTask;
    }
}
