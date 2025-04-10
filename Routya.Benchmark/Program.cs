using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Requests;
using Routya.Core.Extensions;
using System.Reflection;

namespace Routya.Benchmark;

internal class Program
{
    public static void Main() => BenchmarkRunner.Run<DispatcherBenchmarks>();
}

[MemoryDiagnoser]
[GcServer(true)]
[GcForce(true)]
[DisassemblyDiagnoser]
public class DispatcherBenchmarks
{
    private HelloRequest _request;
    public static readonly List<string> Logs = [];
    private IServiceProvider _provider;

    [GlobalSetup]
    public void Setup()
    {
        Logs.Clear();

        var services = new ServiceCollection();

        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, Assembly.GetExecutingAssembly());
        services.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRLoggingBehavior<,>));
        services.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRValidationBehavior<,>));
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });

        _provider = services.BuildServiceProvider();
        _request = new HelloRequest("Benchmark");
    }

    [Benchmark]
    public string Routya_Send()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.Send<HelloRequest, string>(_request);
    }

    [Benchmark]
    public Task<string> Routya_SendAsync()
    {
        using var scope = _provider.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.SendAsync<HelloRequest, string>(_request);
    }

    [Benchmark]
    public Task<string> MediatR_SendAsync()
    {
        using var scope = _provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return mediator.Send(_request);
    }
}

public class HelloRequest(string name) : Routya.Core.Abstractions.IRequest<string>, MediatR.IRequest<string>
{
    public string Name { get; } = name;
}

public class RoutyaAsyncHandler : Routya.Core.Abstractions.IAsyncRequestHandler<HelloRequest, string>
{
    public async Task<string> HandleAsync(HelloRequest request, CancellationToken cancellationToken) => await Task.FromResult($"[Routya] Hello, {request.Name}");
}

public class RoutyaHandler : Routya.Core.Abstractions.IRequestHandler<HelloRequest, string>
{
    public string Handle(HelloRequest request) => $"[Routya] Hello, {request.Name}";
}

public class MediatRHandler : MediatR.IRequestHandler<HelloRequest, string>
{
    public async Task<string> Handle(HelloRequest request, CancellationToken cancellationToken)
        => await Task.FromResult($"[MediatR] Hello, {request.Name}");
}

public class MediatRLoggingBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var result = await next(cancellationToken);
        return result;
    }
}

public class MediatRValidationBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return await next(cancellationToken);
    }
}

public class LoggingBehavior<TRequest, TResponse> : Routya.Core.Abstractions.IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,        
        Core.Abstractions.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next();
        return response;
    }
}

public class ValidationBehavior<TRequest, TResponse> : Routya.Core.Abstractions.IPipelineBehavior<TRequest, TResponse>
{
    public async Task <TResponse> Handle(
        TRequest request,         
        Core.Abstractions.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        return await next();
    }
}
