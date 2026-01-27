using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

public class ParallelNotificationTests
{
    [Fact]
    public async Task Should_Execute_Handlers_In_Parallel()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(ParallelNotificationTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act
        ParallelHandler1.StartTime = null;
        ParallelHandler2.StartTime = null;
        ParallelHandler3.StartTime = null;
        
        await routya.PublishParallelAsync(new ParallelNotification());
        
        // Assert - All should have started within a small window (parallel execution)
        Assert.NotNull(ParallelHandler1.StartTime);
        Assert.NotNull(ParallelHandler2.StartTime);
        Assert.NotNull(ParallelHandler3.StartTime);
        
        var maxDifference = new[] {
            Math.Abs((ParallelHandler1.StartTime!.Value - ParallelHandler2.StartTime!.Value).TotalMilliseconds),
            Math.Abs((ParallelHandler2.StartTime!.Value - ParallelHandler3.StartTime!.Value).TotalMilliseconds),
            Math.Abs((ParallelHandler1.StartTime!.Value - ParallelHandler3.StartTime!.Value).TotalMilliseconds)
        }.Max();
        
        // Handlers should start within 100ms of each other (they run in parallel)
        Assert.True(maxDifference < 100, $"Max difference was {maxDifference}ms, expected < 100ms for parallel execution");
    }
    
    [Fact]
    public async Task Should_Execute_Handlers_Sequentially_With_PublishAsync()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(ParallelNotificationTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act
        SequentialHandler1.ExecutionOrder.Clear();
        await routya.PublishAsync(new SequentialNotification());
        
        // Assert - All three distinct handlers should execute
        Assert.Equal(3, SequentialHandler1.ExecutionOrder.Count);
        Assert.Contains(1, SequentialHandler1.ExecutionOrder);
        Assert.Contains(2, SequentialHandler1.ExecutionOrder);
        Assert.Contains(3, SequentialHandler1.ExecutionOrder);
    }
    
    [Fact]
    public async Task Parallel_Should_Complete_Faster_Than_Sequential()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(ParallelNotificationTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        
        // Act - Sequential
        using (var scope = provider.CreateScope())
        {
            var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
            var sequentialStart = DateTime.UtcNow;
            await routya.PublishAsync(new DelayNotification());
            var sequentialDuration = (DateTime.UtcNow - sequentialStart).TotalMilliseconds;
            
            // Should take at least 150ms (3 handlers x 50ms each)
            Assert.True(sequentialDuration >= 140, $"Sequential took {sequentialDuration}ms, expected >= 140ms");
        }
        
        // Act - Parallel
        using (var scope = provider.CreateScope())
        {
            var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
            var parallelStart = DateTime.UtcNow;
            await routya.PublishParallelAsync(new DelayNotification());
            var parallelDuration = (DateTime.UtcNow - parallelStart).TotalMilliseconds;
            
            // Should take around 50ms (all handlers run concurrently)
            Assert.True(parallelDuration < 100, $"Parallel took {parallelDuration}ms, expected < 100ms");
        }
    }
    
    [Fact]
    public async Task Parallel_Should_ThrowException_When_Handlers_Fail()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(ParallelNotificationTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act & Assert - At least one handler should throw
        FailingHandler1.CallCount = 0;
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await routya.PublishParallelAsync(new FailingNotification())
        );
        
        // At least one handler was invoked (could be 1-3 depending on timing)
        Assert.True(FailingHandler1.CallCount >= 1, $"Expected at least 1 handler called, but got {FailingHandler1.CallCount}");
    }
    
    [Fact]
    public async Task Sequential_Should_Stop_On_First_Exception()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(ParallelNotificationTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act & Assert
        FailingHandler1.CallCount = 0;
        
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await routya.PublishAsync(new FailingNotification())
        );
        
        // Only first handler should have been called (sequential stops on error)
        Assert.Equal(1, FailingHandler1.CallCount);
    }
}

// Test Models
public record ParallelNotification : INotification;
public record SequentialNotification : INotification;
public record DelayNotification : INotification;
public record FailingNotification : INotification;

public class ParallelHandler1 : INotificationHandler<ParallelNotification>
{
    public static DateTime? StartTime { get; set; }
    
    public async Task Handle(ParallelNotification notification, CancellationToken cancellationToken = default)
    {
        StartTime = DateTime.UtcNow;
        await Task.Delay(50, cancellationToken); // Simulate work
    }
}

public class ParallelHandler2 : INotificationHandler<ParallelNotification>
{
    public static DateTime? StartTime { get; set; }
    
    public async Task Handle(ParallelNotification notification, CancellationToken cancellationToken = default)
    {
        StartTime = DateTime.UtcNow;
        await Task.Delay(50, cancellationToken);
    }
}

public class ParallelHandler3 : INotificationHandler<ParallelNotification>
{
    public static DateTime? StartTime { get; set; }
    
    public async Task Handle(ParallelNotification notification, CancellationToken cancellationToken = default)
    {
        StartTime = DateTime.UtcNow;
        await Task.Delay(50, cancellationToken);
    }
}

public class SequentialHandler1 : INotificationHandler<SequentialNotification>
{
    public static List<int> ExecutionOrder { get; } = new();
    
    public async Task Handle(SequentialNotification notification, CancellationToken cancellationToken = default)
    {
        ExecutionOrder.Add(1);
        await Task.Delay(10, cancellationToken);
    }
}

public class SequentialHandler2 : INotificationHandler<SequentialNotification>
{
    public async Task Handle(SequentialNotification notification, CancellationToken cancellationToken = default)
    {
        SequentialHandler1.ExecutionOrder.Add(2);
        await Task.Delay(10, cancellationToken);
    }
}

public class SequentialHandler3 : INotificationHandler<SequentialNotification>
{
    public async Task Handle(SequentialNotification notification, CancellationToken cancellationToken = default)
    {
        SequentialHandler1.ExecutionOrder.Add(3);
        await Task.Delay(10, cancellationToken);
    }
}

public class DelayHandler1 : INotificationHandler<DelayNotification>
{
    public async Task Handle(DelayNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
    }
}

public class DelayHandler2 : INotificationHandler<DelayNotification>
{
    public async Task Handle(DelayNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
    }
}

public class DelayHandler3 : INotificationHandler<DelayNotification>
{
    public async Task Handle(DelayNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
    }
}

public class FailingHandler1 : INotificationHandler<FailingNotification>
{
    public static int CallCount = 0;
    
    public Task Handle(FailingNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref CallCount);
        throw new InvalidOperationException($"Handler 1 failed");
    }
}

public class FailingHandler2 : INotificationHandler<FailingNotification>
{
    public Task Handle(FailingNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref FailingHandler1.CallCount);
        throw new InvalidOperationException($"Handler 2 failed");
    }
}

public class FailingHandler3 : INotificationHandler<FailingNotification>
{
    public Task Handle(FailingNotification notification, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref FailingHandler1.CallCount);
        throw new InvalidOperationException($"Handler 3 failed");
    }
}
