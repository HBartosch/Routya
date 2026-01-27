using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Notifications;

namespace Routya.Test.CoreTests;
public class NotificationDispatcherTests
{
    private readonly IServiceProvider _provider;

    public NotificationDispatcherTests()
    {
        var services = new ServiceCollection();

        // Build notification handler registry manually
        var registry = new Dictionary<Type, List<Core.Extensions.NotificationHandlerInfo>>
        {
            {
                typeof(INotificationHandler<PongNotification>),
                new List<Core.Extensions.NotificationHandlerInfo>
                {
                    new Core.Extensions.NotificationHandlerInfo { ConcreteType = typeof(LogNotificationHandler), Lifetime = ServiceLifetime.Scoped },
                    new Core.Extensions.NotificationHandlerInfo { ConcreteType = typeof(MetricsNotificationHandler), Lifetime = ServiceLifetime.Scoped }
                }
            }
        };
        services.AddSingleton(registry);

        // Register handlers with DI
        services.AddScoped<INotificationHandler<PongNotification>, LogNotificationHandler>();
        services.AddScoped<LogNotificationHandler>();
        services.AddScoped<INotificationHandler<PongNotification>, MetricsNotificationHandler>();
        services.AddScoped<MetricsNotificationHandler>();

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
        public static string Message { get; set; } = null!;

        public Task Handle(PongNotification notification, CancellationToken cancellationToken)
        {
            Message = notification.Message;
            return Task.CompletedTask;
        }
    }

    public class MetricsNotificationHandler : INotificationHandler<PongNotification>
    {
        public static string Message { get; set; } = null!;

        public Task Handle(PongNotification notification, CancellationToken cancellationToken)
        {
            Message = notification.Message;
            return Task.CompletedTask;
        }
    }
}