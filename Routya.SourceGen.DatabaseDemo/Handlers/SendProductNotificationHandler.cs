using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Notifications;

namespace Routya.SourceGen.DatabaseDemo.Handlers;

public class SendProductNotificationHandler : INotificationHandler<ProductCreatedNotification>
{
    public Task Handle(ProductCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  → [EMAIL] Sending notification about new product: {notification.ProductName}");
        return Task.CompletedTask;
    }
}
