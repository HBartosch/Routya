using Routya.Core.Abstractions;
using Routya.WebApi.Demo.Notifications;

namespace Routya.WebApi.Demo.Handlers;

// Singleton handler - logs to console
public class LoggingNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly ILogger<LoggingNotificationHandler> _logger;

    public LoggingNotificationHandler(ILogger<LoggingNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("✅ [SINGLETON - Logging] User created: {UserId} - {Name} ({Email})", 
            notification.UserId, notification.Name, notification.Email);
        return Task.CompletedTask;
    }
}

// Scoped handler - could access DbContext
public class EmailNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly ILogger<EmailNotificationHandler> _logger;

    public EmailNotificationHandler(ILogger<EmailNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📧 [SCOPED - Email] Sending welcome email to: {Email}", notification.Email);
        // In real scenario: await _emailService.SendWelcomeEmailAsync(notification.Email);
        return Task.CompletedTask;
    }
}

// Transient handler - creates new instance each time
public class MetricsNotificationHandler : INotificationHandler<UserCreatedNotification>
{
    private readonly ILogger<MetricsNotificationHandler> _logger;
    private readonly Guid _instanceId = Guid.NewGuid();

    public MetricsNotificationHandler(ILogger<MetricsNotificationHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("📊 [TRANSIENT - Metrics] Instance: {InstanceId} - Tracking user creation: {UserId}", 
            _instanceId, notification.UserId);
        // In real scenario: await _metricsService.TrackEventAsync("user.created", notification.UserId);
        return Task.CompletedTask;
    }
}
