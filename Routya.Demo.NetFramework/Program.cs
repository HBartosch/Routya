using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Demo.NetFramework
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            // Use AddRoutya to automatically configure everything
            services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());

            var provider = services.BuildServiceProvider();

            var dispatcher = provider.GetRequiredService<IRoutya>();

            var result = dispatcher.Send<HelloRequest, string>(new HelloRequest("Framework Console Sync"));
            Console.WriteLine("[SYNC] " + result);

            var asyncResult = dispatcher.SendAsync<HelloRequest, string>(new HelloRequest("Framework Console Async"), CancellationToken.None).Result;
            Console.WriteLine("[ASYNC] " + asyncResult);

            dispatcher.PublishAsync(new HelloNotification("Framework Notification"), CancellationToken.None).Wait();

            Console.WriteLine("Done.");
            Console.ReadLine();
        }
    }

    public class HelloRequest : IRequest<string>
    {
        public string Name { get; }
        public HelloRequest(string name) => Name = name;
    }

    public class HelloRequestHandler : IRequestHandler<HelloRequest, string>, IAsyncRequestHandler<HelloRequest, string>
    {
        public string Handle(HelloRequest request) => "Hello " + request.Name + " [SYNC]";

        public Task<string> HandleAsync(HelloRequest request, CancellationToken cancellation)
        {
            return Task.FromResult("Hello " + request.Name + " [ASYNC]");
        }
    }

    public class HelloNotification : INotification
    {
        public string Message { get; }
        public HelloNotification(string message) => Message = message;
    }

    public class HelloNotificationHandler : INotificationHandler<HelloNotification>
    {
        public Task Handle(HelloNotification notification, CancellationToken cancellationToken)
        {
            Console.WriteLine("[NOTIFICATION] " + notification.Message);
            return Task.CompletedTask;
        }
    }
}