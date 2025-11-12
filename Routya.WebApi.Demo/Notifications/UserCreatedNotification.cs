using Routya.Core.Abstractions;

namespace Routya.WebApi.Demo.Notifications;

public record UserCreatedNotification(int UserId, string Name, string Email) : INotification;
