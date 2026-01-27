using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

public class PipelineBehaviorTests
{
    [Fact]
    public async Task Should_Execute_Pipeline_Behaviors_In_Order()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(PipelineBehaviorTests).Assembly);
        
        // Register behaviors in specific order - only for OrderedPipelineRequest
        services.AddScoped<IPipelineBehavior<OrderedPipelineRequest, string>, FirstBehaviorTyped>();
        services.AddScoped<IPipelineBehavior<OrderedPipelineRequest, string>, SecondBehaviorTyped>();
        services.AddScoped<IPipelineBehavior<OrderedPipelineRequest, string>, ThirdBehaviorTyped>();
        
        var provider = services.BuildServiceProvider();
        
        // Act
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        BehaviorTracker.ExecutionOrder.Clear();
        var result = await routya.SendAsync<OrderedPipelineRequest, string>(new OrderedPipelineRequest());
        
        // Assert
        Assert.Equal("Response", result);
        Assert.Collection(BehaviorTracker.ExecutionOrder,
            item => Assert.Equal("First-Before", item),
            item => Assert.Equal("Second-Before", item),
            item => Assert.Equal("Third-Before", item),
            item => Assert.Equal("Handler", item),
            item => Assert.Equal("Third-After", item),
            item => Assert.Equal("Second-After", item),
            item => Assert.Equal("First-After", item)
        );
    }
    
    [Fact]
    public async Task Should_Pass_CancellationToken_Through_Pipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(PipelineBehaviorTests).Assembly);
        services.AddScoped<IPipelineBehavior<CancellationPipelineRequest, string>, CancellationCheckBehaviorTyped>();
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act
        using var cts = new CancellationTokenSource();
        CancellationCheckBehaviorTyped.TokenWasCancellable = false;
        
        await routya.SendAsync<CancellationPipelineRequest, string>(new CancellationPipelineRequest(), cts.Token);
        
        // Assert
        Assert.True(CancellationCheckBehaviorTyped.TokenWasCancellable);
    }
    
    [Fact]
    public async Task Should_Allow_Behavior_To_Short_Circuit_Pipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(PipelineBehaviorTests).Assembly);
        services.AddScoped<IPipelineBehavior<ShortCircuitPipelineRequest, string>, ShortCircuitBehaviorTyped>();
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act
        BehaviorTracker.ExecutionOrder.Clear();
        var result = await routya.SendAsync<ShortCircuitPipelineRequest, string>(new ShortCircuitPipelineRequest());
        
        // Assert - Handler should not be called
        Assert.Equal("Short-circuited", result);
        Assert.DoesNotContain("Handler", BehaviorTracker.ExecutionOrder);
    }
    
    [Fact]
    public void Should_Work_With_Sync_Handlers_In_Pipeline()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped, typeof(PipelineBehaviorTests).Assembly);
        services.AddScoped<IPipelineBehavior<SyncPipelineRequest, string>, SyncBehaviorTyped>();
        
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        // Act
        BehaviorTracker.ExecutionOrder.Clear();
        var result = routya.Send<SyncPipelineRequest, string>(new SyncPipelineRequest());
        
        // Assert
        Assert.Equal("Sync Response", result);
        Assert.Contains("Sync-Before", BehaviorTracker.ExecutionOrder);
        Assert.Contains("Sync-After", BehaviorTracker.ExecutionOrder);
    }
}

// Test Models
public record OrderedPipelineRequest : IRequest<string>;
public record CancellationPipelineRequest : IRequest<string>;
public record ShortCircuitPipelineRequest : IRequest<string>;
public record SyncPipelineRequest : IRequest<string>;

public class OrderedPipelineRequestHandler : IAsyncRequestHandler<OrderedPipelineRequest, string>
{
    public Task<string> HandleAsync(OrderedPipelineRequest request, CancellationToken cancellationToken)
    {
        BehaviorTracker.ExecutionOrder.Add("Handler");
        return Task.FromResult("Response");
    }
}

public class CancellationPipelineRequestHandler : IAsyncRequestHandler<CancellationPipelineRequest, string>
{
    public Task<string> HandleAsync(CancellationPipelineRequest request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Response");
    }
}

public class ShortCircuitPipelineRequestHandler : IAsyncRequestHandler<ShortCircuitPipelineRequest, string>
{
    public Task<string> HandleAsync(ShortCircuitPipelineRequest request, CancellationToken cancellationToken)
    {
        BehaviorTracker.ExecutionOrder.Add("Handler");
        return Task.FromResult("Response");
    }
}

public class SyncPipelineRequestHandler : IRequestHandler<SyncPipelineRequest, string>
{
    public string Handle(SyncPipelineRequest request)
    {
        return "Sync Response";
    }
}

public static class BehaviorTracker
{
    public static List<string> ExecutionOrder { get; } = new();
}

// Typed behaviors to avoid conflicts between tests
public class FirstBehaviorTyped : IPipelineBehavior<OrderedPipelineRequest, string>
{
    public async Task<string> Handle(OrderedPipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        BehaviorTracker.ExecutionOrder.Add("First-Before");
        var response = await next(cancellationToken);
        BehaviorTracker.ExecutionOrder.Add("First-After");
        return response;
    }
}

public class SecondBehaviorTyped : IPipelineBehavior<OrderedPipelineRequest, string>
{
    public async Task<string> Handle(OrderedPipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        BehaviorTracker.ExecutionOrder.Add("Second-Before");
        var response = await next(cancellationToken);
        BehaviorTracker.ExecutionOrder.Add("Second-After");
        return response;
    }
}

public class ThirdBehaviorTyped : IPipelineBehavior<OrderedPipelineRequest, string>
{
    public async Task<string> Handle(OrderedPipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        BehaviorTracker.ExecutionOrder.Add("Third-Before");
        var response = await next(cancellationToken);
        BehaviorTracker.ExecutionOrder.Add("Third-After");
        return response;
    }
}

public class CancellationCheckBehaviorTyped : IPipelineBehavior<CancellationPipelineRequest, string>
{
    public static bool TokenWasCancellable { get; set; }
    
    public async Task<string> Handle(CancellationPipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        TokenWasCancellable = cancellationToken.CanBeCanceled;
        return await next(cancellationToken);
    }
}

public class ShortCircuitBehaviorTyped : IPipelineBehavior<ShortCircuitPipelineRequest, string>
{
    public Task<string> Handle(ShortCircuitPipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        // Don't call next() - short circuit the pipeline
        return Task.FromResult("Short-circuited");
    }
}

public class SyncBehaviorTyped : IPipelineBehavior<SyncPipelineRequest, string>
{
    public async Task<string> Handle(SyncPipelineRequest request, RequestHandlerDelegate<string> next, CancellationToken cancellationToken)
    {
        BehaviorTracker.ExecutionOrder.Add("Sync-Before");
        var response = await next(cancellationToken);
        BehaviorTracker.ExecutionOrder.Add("Sync-After");
        return response;
    }
}
