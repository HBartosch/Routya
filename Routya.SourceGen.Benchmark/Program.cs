using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using Routya.Generated;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.SourceGen.Benchmark;

internal class Program
{
    // Both classes are run. Pass --filter to select one, for example
    //   dotnet run -c Release -- --filter *NotificationBenchmarks*
    public static void Main(string[] args)
        => BenchmarkSwitcher
            .FromTypes(new[] { typeof(RequestBenchmarks), typeof(NotificationBenchmarks) })
            .Run(args);
}

/// <summary>
/// Shared setup for both benchmark classes.
/// </summary>
/// <remarks>
/// Requests and notifications live in separate classes on purpose. They were previously one class
/// with a single <c>Baseline = true</c> on the request benchmark, so every notification row was
/// divided by a request timing. That made the notification ratios arithmetic rather than a
/// comparison: a ratio of 0.56 meant "a notification takes 56% of the time a request does", not
/// "56% of MediatR". Each class now carries its own baseline, so its Ratio column compares like
/// with like.
/// </remarks>
[MemoryDiagnoser]
[GcServer(true)]
[GcForce(true)]
public abstract class SourceGenBenchmarkBase
{
    protected IServiceProvider _providerMediatR = null!;
    protected IServiceProvider _providerRoutyaV2 = null!;
    protected IServiceProvider _providerRoutyaV3 = null!;
    protected TestRequest _request = null!;
    protected TestNotification _notification = null!;

    [GlobalSetup]
    public void Setup()
    {
        _request = new TestRequest { Value = 42 };
        _notification = new TestNotification { Message = "test" };

        // MediatR setup with 2 pipeline behaviors
        var servicesMediatR = new ServiceCollection();
        servicesMediatR.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRLoggingBehavior<,>));
        servicesMediatR.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(MediatRValidationBehavior<,>));
        servicesMediatR.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        _providerMediatR = servicesMediatR.BuildServiceProvider();

        // Routya v2.0 (Runtime) setup with 2 pipeline behaviors
        var servicesV2 = new ServiceCollection();
        servicesV2.AddTransient(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(RoutyaLoggingBehavior<,>));
        servicesV2.AddTransient(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(RoutyaValidationBehavior<,>));
        servicesV2.AddRoutya(cfg =>
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        }, Assembly.GetExecutingAssembly());
        _providerRoutyaV2 = servicesV2.BuildServiceProvider();

        // Routya v3.0 (Source Generated) setup with 2 pipeline behaviors
        var servicesV3 = new ServiceCollection();
        servicesV3.AddTransient(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(RoutyaLoggingBehavior<,>));
        servicesV3.AddTransient(typeof(Routya.Core.Abstractions.IPipelineBehavior<,>), typeof(RoutyaValidationBehavior<,>));
        servicesV3.AddGeneratedRoutya();  // ⏭️ Source generated!
        _providerRoutyaV3 = servicesV3.BuildServiceProvider();
    }

}

/// <summary>Request dispatch, with MediatR as the baseline.</summary>
public class RequestBenchmarks : SourceGenBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public async Task<string> MediatR_Request()
    {
        using var scope = _providerMediatR.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(_request);
    }

    [Benchmark]
    public async Task<string> RoutyaV2_Request()
    {
        using var scope = _providerRoutyaV2.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        return await dispatcher.SendAsync<TestRequest, string>(_request);
    }

    [Benchmark]
    public async Task<string> RoutyaV3_SourceGen_Request()
    {
        using var scope = _providerRoutyaV3.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IGeneratedRoutya>();
        return await dispatcher.SendAsync(_request);
    }

}

/// <summary>Notification publishing, with MediatR as its own baseline.</summary>
public class NotificationBenchmarks : SourceGenBenchmarkBase
{
    [Benchmark(Baseline = true)]
    public async Task MediatR_Notification()
    {
        using var scope = _providerMediatR.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Publish(_notification);
    }

    [Benchmark]
    public async Task RoutyaV2_Notification()
    {
        using var scope = _providerRoutyaV2.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRoutya>();
        await dispatcher.PublishAsync(_notification);
    }

    [Benchmark]
    public async Task RoutyaV3_SourceGen_Notification()
    {
        using var scope = _providerRoutyaV3.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IGeneratedRoutya>();
        await dispatcher.PublishAsync(_notification);
    }
}

// ==================== PIPELINE BEHAVIORS ====================

// MediatR Pipeline Behaviors
public class MediatRLoggingBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Simulate logging overhead
        _ = request.GetType().Name;
        var response = await next();
        return response;
    }
}

public class MediatRValidationBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Simulate validation overhead
        _ = request != null;
        return await next();
    }
}

// Routya Pipeline Behaviors
public class RoutyaLoggingBehavior<TRequest, TResponse> : Routya.Core.Abstractions.IPipelineBehavior<TRequest, TResponse>
    where TRequest : Routya.Core.Abstractions.IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, Routya.Core.Abstractions.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Simulate logging overhead
        _ = request.GetType().Name;
        var response = await next(cancellationToken);
        return response;
    }
}

public class RoutyaValidationBehavior<TRequest, TResponse> : Routya.Core.Abstractions.IPipelineBehavior<TRequest, TResponse>
    where TRequest : Routya.Core.Abstractions.IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, Routya.Core.Abstractions.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Simulate validation overhead
        _ = request != null;
        return await next(cancellationToken);
    }
}


// ==================== TEST REQUEST/HANDLER ====================

public class TestRequest : Routya.Core.Abstractions.IRequest<string>, MediatR.IRequest<string>
{
    public int Value { get; set; }
}

public class TestRequestHandler : Routya.Core.Abstractions.IAsyncRequestHandler<TestRequest, string>
{
    public Task<string> HandleAsync(TestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"Routya: {request.Value}");
    }
}

public class MediatRTestRequestHandler : MediatR.IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult($"MediatR: {request.Value}");
    }
}

// ==================== TEST NOTIFICATION/HANDLERS ====================

public class TestNotification : Routya.Core.Abstractions.INotification, MediatR.INotification
{
    public string Message { get; set; } = "";
}

public class RoutyaNotificationHandler1 : Routya.Core.Abstractions.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class RoutyaNotificationHandler2 : Routya.Core.Abstractions.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class MediatRNotificationHandler1 : MediatR.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class MediatRNotificationHandler2 : MediatR.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
