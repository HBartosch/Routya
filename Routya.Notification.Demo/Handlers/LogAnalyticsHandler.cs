using Routya.Core.Abstractions;
using Routya.Notification.Demo.Notifications;

namespace Routya.Notification.Demo.Handlers;
public class LogAnalyticsHandler : INotificationHandler<UserRegisteredNotification>
{
    public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(100, cancellationToken);
        Console.WriteLine($"📊 Analytics event logged for {notification.Email}");
    }
}