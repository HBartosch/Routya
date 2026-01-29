using Routya.Core.Abstractions;
using Routya.SourceGen.DatabaseDemo.Notifications;

namespace Routya.SourceGen.DatabaseDemo.Handlers;

public class LogProductCreationHandler : INotificationHandler<ProductCreatedNotification>
{
    public Task Handle(ProductCreatedNotification notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  → [AUDIT LOG] Product created: ID={notification.ProductId}, Name={notification.ProductName}");
        return Task.CompletedTask;
    }
}
