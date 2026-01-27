using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Notifications;
using Routya.Core.Dispatchers.Requests;

namespace Routya.Test.CoreTests;

public class FallbackTests
{
    [Fact]
    public async Task Notification_Should_Fallback_To_GetServices_When_Not_In_Registry()
    {
        // Arrange: Create empty registry (no handlers registered in registry)
        var services = new ServiceCollection();
        var emptyRegistry = new Dictionary<Type, List<Core.Extensions.NotificationHandlerInfo>>();
        services.AddSingleton(emptyRegistry);
        
        // Register handler in DI only (NOT in registry) - register both interface and concrete type
        services.AddScoped<INotificationHandler<TestNotification>, TestNotificationHandler>();
        services.AddScoped<TestNotificationHandler>();
        services.AddSingleton<IRoutyaNotificationDispatcher, CompiledNotificationDispatcher>();
        
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRoutyaNotificationDispatcher>();
        
        // Act: Dispatch notification
        TestNotificationHandler.MessageReceived = null!;
        await dispatcher.PublishAsync(new TestNotification("Fallback Test"));
        
        // Assert: Handler should have been invoked via fallback
        Assert.Equal("Fallback Test", TestNotificationHandler.MessageReceived);
    }
    
    [Fact]
    public async Task Request_Should_Fallback_To_GetService_When_Not_In_Registry()
    {
        // Arrange: Create empty registry (no handlers registered in registry)
        var services = new ServiceCollection();
        var emptyRegistry = new Dictionary<Type, Core.Extensions.RequestHandlerInfo>();
        services.AddSingleton(emptyRegistry);
        
        // Register handler in DI only (NOT in registry)
        services.AddScoped<IAsyncRequestHandler<TestRequest, string>, TestRequestHandler>();
        services.AddSingleton<IRoutyaRequestDispatcher, CompiledRequestInvokerDispatcher>();
        
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRoutyaRequestDispatcher>();
        
        // Act: Dispatch request
        var result = await dispatcher.SendAsync<TestRequest, string>(new TestRequest("Fallback Request"));
        
        // Assert: Handler should have been invoked via fallback
        Assert.Equal("Response: Fallback Request", result);
    }
    
    [Fact]
    public void SyncRequest_Should_Fallback_To_GetService_When_Not_In_Registry()
    {
        // Arrange: Create empty registry (no handlers registered in registry)
        var services = new ServiceCollection();
        var emptyRegistry = new Dictionary<Type, Core.Extensions.RequestHandlerInfo>();
        services.AddSingleton(emptyRegistry);
        
        // Register handler in DI only (NOT in registry)
        services.AddScoped<IRequestHandler<TestSyncRequest, string>, TestSyncRequestHandler>();
        services.AddSingleton<IRoutyaRequestDispatcher, CompiledRequestInvokerDispatcher>();
        
        var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IRoutyaRequestDispatcher>();
        
        // Act: Dispatch sync request
        var result = dispatcher.Send<TestSyncRequest, string>(new TestSyncRequest("Sync Fallback"));
        
        // Assert: Handler should have been invoked via fallback
        Assert.Equal("Sync Response: Sync Fallback", result);
    }
    
    // Test models
    public record TestNotification(string Message) : INotification;
    
    public class TestNotificationHandler : INotificationHandler<TestNotification>
    {
        public static string MessageReceived { get; set; } = null!;
        
        public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
        {
            MessageReceived = notification.Message;
            return Task.CompletedTask;
        }
    }
    
    public record TestRequest(string Input) : IRequest<string>;
    
    public class TestRequestHandler : IAsyncRequestHandler<TestRequest, string>
    {
        public Task<string> HandleAsync(TestRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Response: {request.Input}");
        }
    }
    
    public record TestSyncRequest(string Input) : IRequest<string>;
    
    public class TestSyncRequestHandler : IRequestHandler<TestSyncRequest, string>
    {
        public string Handle(TestSyncRequest request)
        {
            return $"Sync Response: {request.Input}";
        }
    }
}
