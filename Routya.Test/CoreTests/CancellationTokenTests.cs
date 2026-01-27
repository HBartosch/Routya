using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

public class CancellationTokenTests
{
    [Fact]
    public async Task Should_Propagate_Cancellation_To_AsyncRequestHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(CancellationTokenTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        using var cts = new CancellationTokenSource();
        
        // Act
        cts.Cancel();
        
        // Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await routya.SendAsync<CancellableRequest, string>(new CancellableRequest(), cts.Token)
        );
    }
    
    [Fact]
    public async Task Should_Propagate_Cancellation_To_NotificationHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(CancellationTokenTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert - TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await routya.PublishAsync(new CancellableNotification(), cts.Token)
        );
    }
    
    [Fact]
    public async Task Should_Allow_Handler_To_Check_Cancellation()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(CancellationTokenTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        using var cts = new CancellationTokenSource();
        
        // Act
        PollingRequestHandler.TokenWasCancellable = false;
        await routya.SendAsync<PollingRequest, bool>(new PollingRequest(), cts.Token);
        
        // Assert
        Assert.True(PollingRequestHandler.TokenWasCancellable);
    }
    
    [Fact]
    public async Task Should_Complete_When_Token_Not_Cancelled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(CancellationTokenTests).Assembly);
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        using var cts = new CancellationTokenSource();
        // Don't cancel
        
        // Act
        var result = await routya.SendAsync<CancellableRequest, string>(
            new CancellableRequest(), 
            cts.Token
        );
        
        // Assert
        Assert.Equal("Completed", result);
    }
}

// Test Models
public record CancellableRequest : IRequest<string>;
public record CancellableNotification : INotification;
public record PollingRequest : IRequest<bool>;

public class CancellableRequestHandler : IAsyncRequestHandler<CancellableRequest, string>
{
    public async Task<string> HandleAsync(CancellableRequest request, CancellationToken cancellationToken)
    {
        // Simulate some work
        await Task.Delay(50, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return "Completed";
    }
}

public class CancellableNotificationHandler : INotificationHandler<CancellableNotification>
{
    public async Task Handle(CancellableNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

public class PollingRequestHandler : IAsyncRequestHandler<PollingRequest, bool>
{
    public static bool TokenWasCancellable { get; set; }
    
    public Task<bool> HandleAsync(PollingRequest request, CancellationToken cancellationToken)
    {
        TokenWasCancellable = cancellationToken.CanBeCanceled;
        return Task.FromResult(true);
    }
}
