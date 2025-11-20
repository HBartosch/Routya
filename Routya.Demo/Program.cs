using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Demo;

internal class Program
{
    static async Task Main()
    {
        var services = new ServiceCollection();

        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
        services.AddScoped(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));   

        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRoutya>();

        var syncResponse = dispatcher.Send<HelloRequest, string>(new HelloRequest("Sync World"));
        Console.WriteLine(syncResponse);

        var asyncResponse = await dispatcher.SendAsync<HelloRequest, string>(new HelloRequest("Async World"), CancellationToken.None);
        Console.WriteLine(asyncResponse);
    }

    public class HelloRequest(string name) : IRequest<string>
    {
        public string Name { get; } = name;
    }

    public class HelloSyncHandler : IRequestHandler<HelloRequest, string>
    {
        public string Handle(HelloRequest request)
        {
            return $"Hello, {request.Name}!";
        }
    }

    public class HelloAsyncHandler : IAsyncRequestHandler<HelloRequest, string>
    {
        public async Task<string> HandleAsync(HelloRequest request, CancellationToken cancellationToken)
        {
            return await Task.FromResult($"[Async] Hello, {request.Name}!");
        }
    }

    public class LoggingBehavior<TRequest, TResponse> : Routya.Core.Abstractions.IPipelineBehavior<TRequest, TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            Routya.Core.Abstractions.RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Logging] → {typeof(TRequest).Name}");
            var result = await next(cancellationToken);
            Console.WriteLine($"[Logging] ✓ {typeof(TRequest).Name}");
            return result;
        }
    }

    public class ValidationBehavior<TRequest, TResponse> : Routya.Core.Abstractions.IPipelineBehavior<TRequest, TResponse>
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next, 
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"[Validation] ✔ {typeof(TRequest).Name} passed validation.");
            return await next(cancellationToken);
        }
    }
}
