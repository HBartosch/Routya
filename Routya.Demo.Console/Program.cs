using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Demo.Console;

internal class Program
{
    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRoutya>();

        // Send a request
        var syncResult = dispatcher.Send<GreetingRequest, string>(new GreetingRequest("Console Demo"));
        System.Console.WriteLine($"Sync: {syncResult}");

        // Send async request
        var asyncResult = await dispatcher.SendAsync<GreetingRequest, string>(new GreetingRequest("Async Console"), CancellationToken.None);
        System.Console.WriteLine($"Async: {asyncResult}");

        // Publish notification
        await dispatcher.PublishAsync(new AppStartedNotification(), CancellationToken.None);

        System.Console.WriteLine("Demo completed!");
    }

    public class GreetingRequest(string name) : IRequest<string>
    {
        public string Name { get; } = name;
    }

    public class GreetingSyncHandler : IRequestHandler<GreetingRequest, string>
    {
        public string Handle(GreetingRequest request) => $"Hello from {request.Name}! [Sync]";
    }

    public class GreetingAsyncHandler : IAsyncRequestHandler<GreetingRequest, string>
    {
        public async Task<string> HandleAsync(GreetingRequest request, CancellationToken cancellationToken)
        {
            return await Task.FromResult($"Hello from {request.Name}! [Async]");
        }
    }

    public class AppStartedNotification : INotification { }

    public class AppStartedHandler : INotificationHandler<AppStartedNotification>
    {
        public Task Handle(AppStartedNotification notification, CancellationToken cancellationToken)
        {
            System.Console.WriteLine("📢 Notification: Application started successfully!");
            return Task.CompletedTask;
        }
    }
}
