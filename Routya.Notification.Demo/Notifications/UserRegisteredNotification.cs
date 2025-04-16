using Routya.Core.Abstractions;

namespace Routya.Notification.Demo.Notifications;
public class UserRegisteredNotification(string email) : INotification
{
    public string Email { get; } = email;
}