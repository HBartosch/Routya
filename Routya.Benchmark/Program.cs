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
    private IServiceProvider _providerSingleton;
    private IServiceProvider _providerScoped;
    private IServiceProvider _providerTransient;

    [GlobalSetup]
    public void Setup()
    {
        Logs.Clear();

        // Setup for Singleton handlers
        var servicesSingleton = new ServiceCollection();
        servicesSingleton.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Singleton;
        }, Assembly.GetExecutingAssembly());
        servicesSingleton.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        servicesSingleton.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        servicesSingleton.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRLoggingBehavior<,>));
        servicesSingleton.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRValidationBehavior<,>));
        servicesSingleton.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });
        _providerSingleton = servicesSingleton.BuildServiceProvider();

        // Setup for Scoped handlers
        var servicesScoped = new ServiceCollection();
        servicesScoped.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        }, Assembly.GetExecutingAssembly());
        servicesScoped.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        servicesScoped.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        servicesScoped.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRLoggingBehavior<,>));
        servicesScoped.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRValidationBehavior<,>));
        servicesScoped.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });
        _providerScoped = servicesScoped.BuildServiceProvider();

        // Setup for Transient handlers
        var servicesTransient = new ServiceCollection();
        servicesTransient.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Transient;
        }, Assembly.GetExecutingAssembly());
        servicesTransient.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        servicesTransient.AddSingleton(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        servicesTransient.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRLoggingBehavior<,>));
        servicesTransient.AddSingleton(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRValidationBehavior<,>));
        servicesTransient.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
        });
        _providerTransient = servicesTransient.BuildServiceProvider();

        _request = new HelloRequest("Benchmark");
    }

    [Benchmark(Baseline = true)]
    public Task<string> MediatR_SendAsync()
    {
        using var scope = _providerSingleton.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return mediator.Send(_request);
    }

    [Benchmark]
    public string Routya_Singleton_Send()
    {
        using var scope = _providerSingleton.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.Send<HelloRequest, string>(_request);
    }

    [Benchmark]
    public Task<string> Routya_Singleton_SendAsync()
    {
        using var scope = _providerSingleton.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.SendAsync<HelloRequest, string>(_request);
    }

    [Benchmark]
    public string Routya_Scoped_Send()
    {
        using var scope = _providerScoped.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.Send<HelloRequest, string>(_request);
    }

    [Benchmark]
    public Task<string> Routya_Scoped_SendAsync()
    {
        using var scope = _providerScoped.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.SendAsync<HelloRequest, string>(_request);
    }

    [Benchmark]
    public string Routya_Transient_Send()
    {
        using var scope = _providerTransient.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.Send<HelloRequest, string>(_request);
    }

    [Benchmark]
    public Task<string> Routya_Transient_SendAsync()
    {
        using var scope = _providerTransient.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return dispatcher.SendAsync<HelloRequest, string>(_request);
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
