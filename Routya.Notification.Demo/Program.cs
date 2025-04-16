using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using Routya.Notification.Demo.Notifications;
using System.Reflection;

namespace Routya.Notification.Demo;

internal class Program
{
    static async Task Main()
    {
        var services = new ServiceCollection();

        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRoutya>();

        var notification = new UserRegisteredNotification("john.doe@example.com");

        Console.WriteLine("=== Sequential Dispatch ===");
        await dispatcher.PublishAsync(notification);

        Console.WriteLine("=== Parallel Dispatch ===");
        await dispatcher.PublishParallelAsync(notification);
    }
}