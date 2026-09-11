using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Regression tests proving notification handlers registered through plain DI registration are
/// resolved from the dispatch scope on every publish, rather than resolved once from the root
/// provider and cached for the process lifetime.
/// </summary>
public class NotificationScopeTests
{
    [Fact]
    public async Task Scoped_Handler_Registered_Via_Di_Should_Publish_Under_Scope_Validation()
    {
        // Arrange - the registration style the README documents as supported
        var services = new ServiceCollection();
        services.AddScoped<NotificationScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddScoped<INotificationHandler<ScopeValidatedNotification>, ScopeValidatedHandler>();

        // ValidateScopes mirrors the ASP.NET Core Development default
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        ScopeValidatedHandler.InvocationCount = 0;

        // Act
        await routya.PublishAsync(new ScopeValidatedNotification());

        // Assert
        Assert.Equal(1, ScopeValidatedHandler.InvocationCount);
    }

    [Fact]
    public async Task Scoped_Handler_Registered_Via_Di_Should_Be_Resolved_Once_Per_Publish()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<NotificationScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddScoped<INotificationHandler<PerPublishNotification>, PerPublishHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        PerPublishHandler.ObservedDependencyIds.Clear();

        // Act
        await routya.PublishAsync(new PerPublishNotification());
        await routya.PublishAsync(new PerPublishNotification());
        await routya.PublishAsync(new PerPublishNotification());

        // Assert - a distinct scoped dependency per publish
        Assert.Equal(3, PerPublishHandler.ObservedDependencyIds.Count);
        Assert.Equal(3, PerPublishHandler.ObservedDependencyIds.Distinct().Count());
    }

    [Fact]
    public async Task Scoped_Handler_Registered_Via_Di_Should_Never_Observe_A_Disposed_Dependency()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<DisposableNotificationDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddScoped<INotificationHandler<DisposalNotification>, DisposalCheckingNotificationHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        DisposalCheckingNotificationHandler.SawDisposedDependency = false;

        // Act - the first publish scope is disposed before the second publish begins
        await routya.PublishAsync(new DisposalNotification());
        await routya.PublishAsync(new DisposalNotification());

        // Assert
        Assert.False(
            DisposalCheckingNotificationHandler.SawDisposedDependency,
            "A notification handler was reused after its owning dispatch scope had been disposed.");
    }

    [Fact]
    public async Task Scoped_Handler_Registered_Via_Di_Should_Be_Resolved_Per_Parallel_Publish()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<NotificationScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddScoped<INotificationHandler<ParallelPublishNotification>, ParallelPublishHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        ParallelPublishHandler.ObservedDependencyIds.Clear();

        // Act
        await routya.PublishParallelAsync(new ParallelPublishNotification());
        await routya.PublishParallelAsync(new ParallelPublishNotification());

        // Assert
        Assert.Equal(2, ParallelPublishHandler.ObservedDependencyIds.Count);
        Assert.Equal(2, ParallelPublishHandler.ObservedDependencyIds.Distinct().Count());
    }

    [Fact]
    public async Task Registry_Registered_Scoped_Handler_Should_Still_Be_Resolved_Per_Publish()
    {
        // Arrange - the recommended registration path, guarding against a regression there
        var services = new ServiceCollection();
        services.AddScoped<NotificationScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaNotificationHandler<RegistryPathNotification, RegistryPathHandler>(ServiceLifetime.Scoped);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        RegistryPathHandler.ObservedDependencyIds.Clear();

        // Act
        await routya.PublishAsync(new RegistryPathNotification());
        await routya.PublishAsync(new RegistryPathNotification());

        // Assert
        Assert.Equal(2, RegistryPathHandler.ObservedDependencyIds.Count);
        Assert.Equal(2, RegistryPathHandler.ObservedDependencyIds.Distinct().Count());
    }
}

// Test models
public record ScopeValidatedNotification : INotification;
public record PerPublishNotification : INotification;
public record DisposalNotification : INotification;
public record ParallelPublishNotification : INotification;
public record RegistryPathNotification : INotification;

public class NotificationScopedDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class DisposableNotificationDependency : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

public class ScopeValidatedHandler : INotificationHandler<ScopeValidatedNotification>
{
    public static int InvocationCount { get; set; }

    private readonly NotificationScopedDependency _dependency;

    public ScopeValidatedHandler(NotificationScopedDependency dependency) => _dependency = dependency;

    public Task Handle(ScopeValidatedNotification notification, CancellationToken cancellationToken = default)
    {
        _ = _dependency.Id;
        InvocationCount++;
        return Task.CompletedTask;
    }
}

public class PerPublishHandler : INotificationHandler<PerPublishNotification>
{
    public static List<Guid> ObservedDependencyIds { get; } = new();

    private readonly NotificationScopedDependency _dependency;

    public PerPublishHandler(NotificationScopedDependency dependency) => _dependency = dependency;

    public Task Handle(PerPublishNotification notification, CancellationToken cancellationToken = default)
    {
        ObservedDependencyIds.Add(_dependency.Id);
        return Task.CompletedTask;
    }
}

public class ParallelPublishHandler : INotificationHandler<ParallelPublishNotification>
{
    public static List<Guid> ObservedDependencyIds { get; } = new();

    private readonly NotificationScopedDependency _dependency;

    public ParallelPublishHandler(NotificationScopedDependency dependency) => _dependency = dependency;

    public Task Handle(ParallelPublishNotification notification, CancellationToken cancellationToken = default)
    {
        ObservedDependencyIds.Add(_dependency.Id);
        return Task.CompletedTask;
    }
}

public class RegistryPathHandler : INotificationHandler<RegistryPathNotification>
{
    public static List<Guid> ObservedDependencyIds { get; } = new();

    private readonly NotificationScopedDependency _dependency;

    public RegistryPathHandler(NotificationScopedDependency dependency) => _dependency = dependency;

    public Task Handle(RegistryPathNotification notification, CancellationToken cancellationToken = default)
    {
        ObservedDependencyIds.Add(_dependency.Id);
        return Task.CompletedTask;
    }
}

public class DisposalCheckingNotificationHandler : INotificationHandler<DisposalNotification>
{
    public static bool SawDisposedDependency { get; set; }

    private readonly DisposableNotificationDependency _dependency;

    public DisposalCheckingNotificationHandler(DisposableNotificationDependency dependency)
        => _dependency = dependency;

    public Task Handle(DisposalNotification notification, CancellationToken cancellationToken = default)
    {
        if (_dependency.IsDisposed)
        {
            SawDisposedDependency = true;
        }

        return Task.CompletedTask;
    }
}
