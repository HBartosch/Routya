using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Regression tests proving pipeline behaviors are resolved from the dispatch scope on every
/// dispatch rather than resolved once and cached for the process lifetime.
/// </summary>
public class BehaviorScopeTests
{
    [Fact]
    public async Task Scoped_Behavior_Should_Be_Resolved_Once_Per_Dispatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<BehaviorScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(BehaviorScopeTests).Assembly);
        services.AddScoped<IPipelineBehavior<BehaviorScopeRequest, string>, ScopeCapturingBehavior>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        ScopeCapturingBehavior.ObservedDependencyIds.Clear();

        // Act - three dispatches, each of which creates its own dispatch scope
        await routya.SendAsync<BehaviorScopeRequest, string>(new BehaviorScopeRequest());
        await routya.SendAsync<BehaviorScopeRequest, string>(new BehaviorScopeRequest());
        await routya.SendAsync<BehaviorScopeRequest, string>(new BehaviorScopeRequest());

        // Assert - the behavior must see a distinct scoped dependency each time
        Assert.Equal(3, ScopeCapturingBehavior.ObservedDependencyIds.Count);
        Assert.Equal(3, ScopeCapturingBehavior.ObservedDependencyIds.Distinct().Count());
    }

    [Fact]
    public async Task Scoped_Behavior_Should_Never_Observe_A_Disposed_Dependency()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DisposableBehaviorDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(BehaviorScopeTests).Assembly);
        services.AddScoped<IPipelineBehavior<DisposalScopeRequest, string>, DisposalCheckingBehavior>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        DisposalCheckingBehavior.SawDisposedDependency = false;

        // Act - the first dispatch scope is disposed before the second dispatch begins
        await routya.SendAsync<DisposalScopeRequest, string>(new DisposalScopeRequest());
        await routya.SendAsync<DisposalScopeRequest, string>(new DisposalScopeRequest());

        // Assert
        Assert.False(
            DisposalCheckingBehavior.SawDisposedDependency,
            "A pipeline behavior was reused after its owning dispatch scope had been disposed.");
    }

    [Fact]
    public async Task Behaviors_Should_Not_Leak_Between_Service_Providers()
    {
        // Arrange - the first container registers a behavior, the second deliberately does not
        var withBehavior = new ServiceCollection();
        withBehavior.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(BehaviorScopeTests).Assembly);
        withBehavior.AddScoped<IPipelineBehavior<ContainerLeakRequest, string>, CountingLeakBehavior>();
        var providerWithBehavior = withBehavior.BuildServiceProvider();

        var withoutBehavior = new ServiceCollection();
        withoutBehavior.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(BehaviorScopeTests).Assembly);
        var providerWithoutBehavior = withoutBehavior.BuildServiceProvider();

        CountingLeakBehavior.InvocationCount = 0;

        // Act
        await providerWithBehavior
            .GetRequiredService<IRoutya>()
            .SendAsync<ContainerLeakRequest, string>(new ContainerLeakRequest());

        var countAfterFirstContainer = CountingLeakBehavior.InvocationCount;

        await providerWithoutBehavior
            .GetRequiredService<IRoutya>()
            .SendAsync<ContainerLeakRequest, string>(new ContainerLeakRequest());

        // Assert - the second container has no behavior registered, so the count must not move
        Assert.Equal(1, countAfterFirstContainer);
        Assert.Equal(1, CountingLeakBehavior.InvocationCount);
    }

    [Fact]
    public void Scoped_Behavior_Should_Be_Resolved_Once_Per_Sync_Dispatch()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<BehaviorScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(BehaviorScopeTests).Assembly);
        services.AddScoped<IPipelineBehavior<SyncBehaviorScopeRequest, string>, SyncScopeCapturingBehavior>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        SyncScopeCapturingBehavior.ObservedDependencyIds.Clear();

        // Act
        routya.Send<SyncBehaviorScopeRequest, string>(new SyncBehaviorScopeRequest());
        routya.Send<SyncBehaviorScopeRequest, string>(new SyncBehaviorScopeRequest());

        // Assert
        Assert.Equal(2, SyncScopeCapturingBehavior.ObservedDependencyIds.Count);
        Assert.Equal(2, SyncScopeCapturingBehavior.ObservedDependencyIds.Distinct().Count());
    }
}

// Test models
public record BehaviorScopeRequest : IRequest<string>;
public record DisposalScopeRequest : IRequest<string>;
public record ContainerLeakRequest : IRequest<string>;
public record SyncBehaviorScopeRequest : IRequest<string>;

public class BehaviorScopedDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class DisposableBehaviorDependency : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

public class BehaviorScopeRequestHandler : IAsyncRequestHandler<BehaviorScopeRequest, string>
{
    public Task<string> HandleAsync(BehaviorScopeRequest request, CancellationToken cancellationToken)
        => Task.FromResult("ok");
}

public class DisposalScopeRequestHandler : IAsyncRequestHandler<DisposalScopeRequest, string>
{
    public Task<string> HandleAsync(DisposalScopeRequest request, CancellationToken cancellationToken)
        => Task.FromResult("ok");
}

public class ContainerLeakRequestHandler : IAsyncRequestHandler<ContainerLeakRequest, string>
{
    public Task<string> HandleAsync(ContainerLeakRequest request, CancellationToken cancellationToken)
        => Task.FromResult("ok");
}

public class SyncBehaviorScopeRequestHandler : IRequestHandler<SyncBehaviorScopeRequest, string>
{
    public string Handle(SyncBehaviorScopeRequest request) => "ok";
}

public class ScopeCapturingBehavior : IPipelineBehavior<BehaviorScopeRequest, string>
{
    public static List<Guid> ObservedDependencyIds { get; } = new();

    private readonly BehaviorScopedDependency _dependency;

    public ScopeCapturingBehavior(BehaviorScopedDependency dependency) => _dependency = dependency;

    public Task<string> Handle(
        BehaviorScopeRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        ObservedDependencyIds.Add(_dependency.Id);
        return next(cancellationToken);
    }
}

public class SyncScopeCapturingBehavior : IPipelineBehavior<SyncBehaviorScopeRequest, string>
{
    public static List<Guid> ObservedDependencyIds { get; } = new();

    private readonly BehaviorScopedDependency _dependency;

    public SyncScopeCapturingBehavior(BehaviorScopedDependency dependency) => _dependency = dependency;

    public Task<string> Handle(
        SyncBehaviorScopeRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        ObservedDependencyIds.Add(_dependency.Id);
        return next(cancellationToken);
    }
}

public class DisposalCheckingBehavior : IPipelineBehavior<DisposalScopeRequest, string>
{
    public static bool SawDisposedDependency { get; set; }

    private readonly DisposableBehaviorDependency _dependency;

    public DisposalCheckingBehavior(DisposableBehaviorDependency dependency) => _dependency = dependency;

    public Task<string> Handle(
        DisposalScopeRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        if (_dependency.IsDisposed)
        {
            SawDisposedDependency = true;
        }

        return next(cancellationToken);
    }
}

public class CountingLeakBehavior : IPipelineBehavior<ContainerLeakRequest, string>
{
    public static int InvocationCount { get; set; }

    public Task<string> Handle(
        ContainerLeakRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        return next(cancellationToken);
    }
}
