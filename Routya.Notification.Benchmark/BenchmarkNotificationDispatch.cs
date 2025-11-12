using BenchmarkDotNet.Attributes;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using System.Reflection;

namespace Routya.Notification.Benchmark;

[MemoryDiagnoser]
[GcServer(true)]
[GcForce(true)]
[DisassemblyDiagnoser]
public class BenchmarkNotificationDispatch
{
    private IRoutya _routyaSingleton = default!;
    private IRoutya _routyaScoped = default!;
    private IRoutya _routyaTransient = default!;
    private IMediator _mediator = default!;
    private TestNotification _notification = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Setup for Singleton handlers
        var servicesSingleton = new ServiceCollection();
        servicesSingleton.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Singleton;
        }, Assembly.GetExecutingAssembly());
        var providerSingleton = servicesSingleton.BuildServiceProvider();
        _routyaSingleton = providerSingleton.GetRequiredService<IRoutya>();

        // Setup for Scoped handlers
        var servicesScoped = new ServiceCollection();
        servicesScoped.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        }, Assembly.GetExecutingAssembly());
        var providerScoped = servicesScoped.BuildServiceProvider();
        _routyaScoped = providerScoped.GetRequiredService<IRoutya>();

        // Setup for Transient handlers
        var servicesTransient = new ServiceCollection();
        servicesTransient.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Transient;
        }, Assembly.GetExecutingAssembly());
        var providerTransient = servicesTransient.BuildServiceProvider();
        _routyaTransient = providerTransient.GetRequiredService<IRoutya>();

        // Setup MediatR
        var servicesMediatR = new ServiceCollection();
        servicesMediatR.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
        var providerMediatR = servicesMediatR.BuildServiceProvider();
        _mediator = providerMediatR.GetRequiredService<IMediator>();

        _notification = new TestNotification("bench@test.com");
    }

    [Benchmark(Baseline = true)]
    public async Task MediatR_Publish() => await _mediator.Publish(_notification);

    [Benchmark]
    public async Task Routya_Singleton_Sequential() => await _routyaSingleton.PublishAsync(_notification);

    [Benchmark]
    public async Task Routya_Singleton_Parallel() => await _routyaSingleton.PublishParallelAsync(_notification);

    [Benchmark]
    public async Task Routya_Scoped_Sequential() => await _routyaScoped.PublishAsync(_notification);

    [Benchmark]
    public async Task Routya_Scoped_Parallel() => await _routyaScoped.PublishParallelAsync(_notification);

    [Benchmark]
    public async Task Routya_Transient_Sequential() => await _routyaTransient.PublishAsync(_notification);

    [Benchmark]
    public async Task Routya_Transient_Parallel() => await _routyaTransient.PublishParallelAsync(_notification);
}

public class TestNotification(string email) : Routya.Core.Abstractions.INotification, MediatR.INotification
{
    public string Email { get; } = email;
}

public class RoutyaHandler1 : Routya.Core.Abstractions.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class RoutyaHandler2 : Routya.Core.Abstractions.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class MediatRHandler1 : MediatR.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class MediatRHandler2 : MediatR.INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}