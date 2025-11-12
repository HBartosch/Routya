using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

public class ManualRegistrationTests
{
    [Fact]
    public async Task Should_Register_NotificationHandlers_Manually_With_Different_Lifetimes()
    {
        // Arrange
        var services = new ServiceCollection();

        // Manually register notification handlers with different lifetimes
        services.AddRoutyaNotificationHandler<TestNotification, SingletonHandler>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<TestNotification, ScopedHandler>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<TestNotification, TransientHandler>(ServiceLifetime.Transient);

        // Register Routya without assembly scanning
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);

        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        TestHandler.CallCount = 0;
        await routya.PublishAsync(new TestNotification("test"));

        // Assert
        Assert.Equal(3, TestHandler.CallCount); // All three handlers should be called
    }

    [Fact]
    public async Task Should_Register_RequestHandler_Manually()
    {
        // Arrange
        var services = new ServiceCollection();

        // Manually register request handler
        services.AddRoutyaRequestHandler<TestRequest, string, TestRequestHandler>(ServiceLifetime.Scoped);

        // Register Routya without assembly scanning
        services.AddRoutya();

        var provider = services.BuildServiceProvider();

        // Act
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        var result = routya.Send<TestRequest, string>(new TestRequest("Hello"));

        // Assert
        Assert.Equal("Hello from handler", result);
    }

    [Fact]
    public async Task Should_Register_AsyncRequestHandler_Manually()
    {
        // Arrange
        var services = new ServiceCollection();

        // Manually register async request handler
        services.AddRoutyaAsyncRequestHandler<TestAsyncRequest, int, TestAsyncRequestHandler>(ServiceLifetime.Singleton);

        // Register Routya without assembly scanning
        services.AddRoutya();

        var provider = services.BuildServiceProvider();

        // Act
        var routya = provider.GetRequiredService<IRoutya>();
        var result = await routya.SendAsync<TestAsyncRequest, int>(new TestAsyncRequest(42));

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Should_Use_Scoped_Lifetime_By_Default()
    {
        // Arrange
        var services = new ServiceCollection();

        // Register without specifying lifetime (should default to Scoped)
        services.AddRoutyaNotificationHandler<TestNotification, ScopedHandler>();

        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);

        var provider = services.BuildServiceProvider();

        // Act
        TestHandler.CallCount = 0;
        
        using (var scope1 = provider.CreateScope())
        {
            var routya1 = scope1.ServiceProvider.GetRequiredService<IRoutya>();
            await routya1.PublishAsync(new TestNotification("test1"));
        }

        using (var scope2 = provider.CreateScope())
        {
            var routya2 = scope2.ServiceProvider.GetRequiredService<IRoutya>();
            await routya2.PublishAsync(new TestNotification("test2"));
        }

        // Assert - both scopes should have been called
        Assert.Equal(2, TestHandler.CallCount);
    }
}

// Test types
public record TestNotification(string Message) : INotification;
public record TestRequest(string Value) : IRequest<string>;
public record TestAsyncRequest(int Value) : IRequest<int>;

public abstract class TestHandler
{
    public static int CallCount = 0;
}

public class SingletonHandler : TestHandler, INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.CompletedTask;
    }
}

public class ScopedHandler : TestHandler, INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.CompletedTask;
    }
}

public class TransientHandler : TestHandler, INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.CompletedTask;
    }
}

public class TestRequestHandler : IRequestHandler<TestRequest, string>
{
    public string Handle(TestRequest request) => $"{request.Value} from handler";
}

public class TestAsyncRequestHandler : IAsyncRequestHandler<TestAsyncRequest, int>
{
    public Task<int> HandleAsync(TestAsyncRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(request.Value);
    }
}
