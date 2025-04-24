using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Notifications;
using Routya.Core.Dispatchers.Requests;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Demo.NetFramework
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var services = new ServiceCollection();

            services.AddSingleton<IRequestHandler<HelloRequest, string>, HelloRequestHandler>();
            services.AddSingleton<IAsyncRequestHandler<HelloRequest, string>, HelloRequestHandler>();

            services.AddSingleton<INotificationHandler<HelloNotification>, HelloNotificationHandler>();
            services.AddSingleton<IRoutyaRequestDispatcher, CompiledRequestInvokerDispatcher>();
            services.AddSingleton<IRoutyaNotificationDispatcher, CompiledNotificationDispatcher>();

            var provider = services.BuildServiceProvider();

            var requestDispatcher = provider.GetRequiredService<IRoutyaRequestDispatcher>();
            var notificationDispatcher = provider.GetRequiredService<IRoutyaNotificationDispatcher>();

            var result = requestDispatcher.Send<HelloRequest, string>(new HelloRequest("Framework Console Sync"));
            Console.WriteLine("[SYNC] " + result);

            var asyncResult = requestDispatcher.SendAsync<HelloRequest, string>(new HelloRequest("Framework Console Async")).Result;
            Console.WriteLine("[ASYNC] " + asyncResult);

            notificationDispatcher.PublishAsync(new HelloNotification("Framework Notification")).Wait();

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