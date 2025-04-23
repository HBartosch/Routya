using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Notifications;

namespace Routya.Test;
public class NotificationDispatcherTests
{
    private readonly IServiceProvider _provider;

    public NotificationDispatcherTests()
    {
        var services = new ServiceCollection();

        // Register notification handlers
        services.AddScoped<INotificationHandler<PongNotification>, LogNotificationHandler>();
        services.AddScoped<INotificationHandler<PongNotification>, MetricsNotificationHandler>();

        // Register dispatcher
        services.AddSingleton<IRoutyaNotificationDispatcher, CompiledNotificationDispatcher>();

        _provider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Should_Publish_Notification_To_All_Handlers()
    {
        var notificationMessage = "Notify";
        LogNotificationHandler.Message = string.Empty;
        MetricsNotificationHandler.Message = string.Empty;

        var dispatcher = _provider.GetRequiredService<IRoutyaNotificationDispatcher>();
        var notification = new PongNotification(notificationMessage);

        await dispatcher.PublishAsync(notification);

        Assert.True(LogNotificationHandler.Message == notificationMessage);
        Assert.True(MetricsNotificationHandler.Message == notificationMessage);
    }

    public record PongNotification(string Message) : INotification;

    public class LogNotificationHandler : INotificationHandler<PongNotification>
    {
        public static string Message { get; set; }

        public Task Handle(PongNotification notification, CancellationToken cancellationToken)
        {
            Message = notification.Message;
            return Task.CompletedTask;
        }
    }

    public class MetricsNotificationHandler : INotificationHandler<PongNotification>
    {
        public static string Message { get; set; }

        public Task Handle(PongNotification notification, CancellationToken cancellationToken)
        {
            Message = notification.Message;
            return Task.CompletedTask;
        }
    }
}