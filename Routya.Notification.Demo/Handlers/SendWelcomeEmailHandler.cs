using Routya.Core.Abstractions;
using Routya.Notification.Demo.Notifications;

namespace Routya.Notification.Demo.Handlers;
public class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredNotification>
{
    public async Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(200, cancellationToken);
        Console.WriteLine($"📧 Welcome email sent to {notification.Email}");
    }
}