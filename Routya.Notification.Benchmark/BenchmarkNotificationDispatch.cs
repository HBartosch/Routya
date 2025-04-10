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
    private IRoutya _routyaCompiled = default!;
    private IMediator _mediator = default!;
    private TestNotification _notification = default!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();

        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Root, Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        var provider = services.BuildServiceProvider();

        _routyaCompiled = provider.GetRequiredService<IRoutya>();
        _mediator = provider.GetRequiredService<IMediator>();
        _notification = new TestNotification("bench@test.com");
    }

    [Benchmark]
    public async Task MediatR_Publish() => await _mediator.Publish(_notification);

    [Benchmark]
    public async Task RoutyaCompiled_Sequential() => await _routyaCompiled.PublishAsync(_notification);

    [Benchmark]
    public async Task RoutyaCompiled_Parallel() => await _routyaCompiled.PublishParallelAsync(_notification);
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