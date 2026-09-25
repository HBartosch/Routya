using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Parallel publishing runs every handler concurrently, so handlers must not be handed the same
/// scoped dependency. A DbContext is the common case and throws
/// "A second operation was started on this context" when two handlers use it at once.
/// </summary>
/// <remarks>
/// Sequential publishing deliberately keeps a single shared scope. Handlers run one after another,
/// so there is no concurrency hazard, and sharing is useful when a later handler commits work a
/// earlier one staged. The two strategies therefore behave differently on purpose, and both
/// behaviours are pinned here.
/// </remarks>
public class ParallelPublishScopeTests
{
    [Fact]
    public async Task Parallel_Publish_Gives_Each_Handler_Its_Own_Scope()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ParallelScopeMarker>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaNotificationHandler<ParallelScopeNotification, ParallelScopeHandlerOne>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<ParallelScopeNotification, ParallelScopeHandlerTwo>(ServiceLifetime.Scoped);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        ParallelScopeTracker.Observed.Clear();

        // Act
        await routya.PublishParallelAsync(new ParallelScopeNotification());

        // Assert - both handlers ran, and each saw its own scoped dependency
        Assert.Equal(2, ParallelScopeTracker.Observed.Count);
        Assert.Equal(2, ParallelScopeTracker.Observed.Distinct().Count());
    }

    [Fact]
    public async Task Sequential_Publish_Still_Shares_One_Scope()
    {
        // Arrange - the deliberate difference from parallel, pinned so it cannot drift
        var services = new ServiceCollection();
        services.AddScoped<ParallelScopeMarker>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaNotificationHandler<SequentialScopeNotification, SequentialScopeHandlerOne>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<SequentialScopeNotification, SequentialScopeHandlerTwo>(ServiceLifetime.Scoped);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        SequentialScopeTracker.Observed.Clear();

        // Act
        await routya.PublishAsync(new SequentialScopeNotification());

        // Assert - both handlers ran, sharing a single scoped dependency
        Assert.Equal(2, SequentialScopeTracker.Observed.Count);
        Assert.Single(SequentialScopeTracker.Observed.Distinct());
    }

    [Fact]
    public async Task Parallel_Publish_Gives_Each_Handler_Its_Own_Scope_With_Plain_Di_Registration()
    {
        // Arrange - handlers registered straight against IServiceCollection rather than the registry
        var services = new ServiceCollection();
        services.AddScoped<ParallelScopeMarker>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddScoped<INotificationHandler<PlainDiParallelNotification>, PlainDiParallelHandlerOne>();
        services.AddScoped<INotificationHandler<PlainDiParallelNotification>, PlainDiParallelHandlerTwo>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        PlainDiParallelTracker.Observed.Clear();

        // Act
        await routya.PublishParallelAsync(new PlainDiParallelNotification());

        // Assert
        Assert.Equal(2, PlainDiParallelTracker.Observed.Count);
        Assert.Equal(2, PlainDiParallelTracker.Observed.Distinct().Count());
    }

    [Fact]
    public async Task Parallel_Publish_Still_Invokes_Every_Handler_Across_Repeated_Publishes()
    {
        var services = new ServiceCollection();
        services.AddScoped<ParallelScopeMarker>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaNotificationHandler<ParallelScopeNotification, ParallelScopeHandlerOne>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<ParallelScopeNotification, ParallelScopeHandlerTwo>(ServiceLifetime.Scoped);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        ParallelScopeTracker.Observed.Clear();

        await routya.PublishParallelAsync(new ParallelScopeNotification());
        await routya.PublishParallelAsync(new ParallelScopeNotification());
        await routya.PublishParallelAsync(new ParallelScopeNotification());

        // Three publishes, two handlers each, and every scope distinct
        Assert.Equal(6, ParallelScopeTracker.Observed.Count);
        Assert.Equal(6, ParallelScopeTracker.Observed.Distinct().Count());
    }
}

// Test models
public record ParallelScopeNotification : INotification;
public record SequentialScopeNotification : INotification;
public record PlainDiParallelNotification : INotification;

public class ParallelScopeMarker
{
    public Guid Id { get; } = Guid.NewGuid();
}

public static class ParallelScopeTracker
{
    public static ConcurrentBag<Guid> Observed { get; } = new();
}

public static class SequentialScopeTracker
{
    public static ConcurrentBag<Guid> Observed { get; } = new();
}

public static class PlainDiParallelTracker
{
    public static ConcurrentBag<Guid> Observed { get; } = new();
}

public class ParallelScopeHandlerOne : INotificationHandler<ParallelScopeNotification>
{
    private readonly ParallelScopeMarker _marker;

    public ParallelScopeHandlerOne(ParallelScopeMarker marker) => _marker = marker;

    public async Task Handle(ParallelScopeNotification notification, CancellationToken cancellationToken = default)
    {
        // Yield so both handlers are genuinely in flight together
        await Task.Delay(20, CancellationToken.None);
        ParallelScopeTracker.Observed.Add(_marker.Id);
    }
}

public class ParallelScopeHandlerTwo : INotificationHandler<ParallelScopeNotification>
{
    private readonly ParallelScopeMarker _marker;

    public ParallelScopeHandlerTwo(ParallelScopeMarker marker) => _marker = marker;

    public async Task Handle(ParallelScopeNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(20, CancellationToken.None);
        ParallelScopeTracker.Observed.Add(_marker.Id);
    }
}

public class SequentialScopeHandlerOne : INotificationHandler<SequentialScopeNotification>
{
    private readonly ParallelScopeMarker _marker;

    public SequentialScopeHandlerOne(ParallelScopeMarker marker) => _marker = marker;

    public Task Handle(SequentialScopeNotification notification, CancellationToken cancellationToken = default)
    {
        SequentialScopeTracker.Observed.Add(_marker.Id);
        return Task.CompletedTask;
    }
}

public class SequentialScopeHandlerTwo : INotificationHandler<SequentialScopeNotification>
{
    private readonly ParallelScopeMarker _marker;

    public SequentialScopeHandlerTwo(ParallelScopeMarker marker) => _marker = marker;

    public Task Handle(SequentialScopeNotification notification, CancellationToken cancellationToken = default)
    {
        SequentialScopeTracker.Observed.Add(_marker.Id);
        return Task.CompletedTask;
    }
}

public class PlainDiParallelHandlerOne : INotificationHandler<PlainDiParallelNotification>
{
    private readonly ParallelScopeMarker _marker;

    public PlainDiParallelHandlerOne(ParallelScopeMarker marker) => _marker = marker;

    public async Task Handle(PlainDiParallelNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(20, CancellationToken.None);
        PlainDiParallelTracker.Observed.Add(_marker.Id);
    }
}

public class PlainDiParallelHandlerTwo : INotificationHandler<PlainDiParallelNotification>
{
    private readonly ParallelScopeMarker _marker;

    public PlainDiParallelHandlerTwo(ParallelScopeMarker marker) => _marker = marker;

    public async Task Handle(PlainDiParallelNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(20, CancellationToken.None);
        PlainDiParallelTracker.Observed.Add(_marker.Id);
    }
}
