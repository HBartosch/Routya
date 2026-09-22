using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Handlers registered straight against IServiceCollection map an interface to an implementation
/// and do not register the implementation type itself. Resolving such a handler by its concrete
/// type therefore throws, so dispatch has to keep going through the interface on every call, not
/// just the first.
/// </summary>
public class InterfaceOnlyRegistrationTests
{
    [Fact]
    public async Task Async_Handler_Registered_Only_By_Interface_Dispatches_On_Every_Call()
    {
        // Arrange - note the absence of services.AddScoped<InterfaceOnlyAsyncHandler>()
        var services = new ServiceCollection();
        services.AddRoutya();
        services.AddScoped<IAsyncRequestHandler<InterfaceOnlyRequest, string>, InterfaceOnlyAsyncHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        // Act - the first call goes through discovery, later calls must not switch to a strategy
        // that resolves the concrete type
        var first = await routya.SendAsync<InterfaceOnlyRequest, string>(new InterfaceOnlyRequest("a"));
        var second = await routya.SendAsync<InterfaceOnlyRequest, string>(new InterfaceOnlyRequest("b"));
        var third = await routya.SendAsync<InterfaceOnlyRequest, string>(new InterfaceOnlyRequest("c"));

        // Assert
        Assert.Equal("handled:a", first);
        Assert.Equal("handled:b", second);
        Assert.Equal("handled:c", third);
    }

    [Fact]
    public void Sync_Handler_Registered_Only_By_Interface_Dispatches_On_Every_Call()
    {
        var services = new ServiceCollection();
        services.AddRoutya();
        services.AddScoped<IRequestHandler<InterfaceOnlySyncRequest, string>, InterfaceOnlySyncHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        var first = routya.Send<InterfaceOnlySyncRequest, string>(new InterfaceOnlySyncRequest("a"));
        var second = routya.Send<InterfaceOnlySyncRequest, string>(new InterfaceOnlySyncRequest("b"));
        var third = routya.Send<InterfaceOnlySyncRequest, string>(new InterfaceOnlySyncRequest("c"));

        Assert.Equal("sync:a", first);
        Assert.Equal("sync:b", second);
        Assert.Equal("sync:c", third);
    }

    [Fact]
    public async Task Notification_Handler_Registered_Only_By_Interface_Runs_On_Every_Publish()
    {
        var services = new ServiceCollection();
        services.AddRoutya();
        services.AddScoped<INotificationHandler<InterfaceOnlyNotification>, InterfaceOnlyNotificationHandler>();

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var routya = provider.GetRequiredService<IRoutya>();

        InterfaceOnlyNotificationHandler.Received.Clear();

        await routya.PublishAsync(new InterfaceOnlyNotification("a"));
        await routya.PublishAsync(new InterfaceOnlyNotification("b"));
        await routya.PublishAsync(new InterfaceOnlyNotification("c"));

        Assert.Equal(new[] { "a", "b", "c" }, InterfaceOnlyNotificationHandler.Received);
    }
}

// Test models
public record InterfaceOnlyRequest(string Value) : IRequest<string>;
public record InterfaceOnlySyncRequest(string Value) : IRequest<string>;
public record InterfaceOnlyNotification(string Value) : INotification;

public class InterfaceOnlyAsyncHandler : IAsyncRequestHandler<InterfaceOnlyRequest, string>
{
    public Task<string> HandleAsync(InterfaceOnlyRequest request, CancellationToken cancellationToken)
        => Task.FromResult($"handled:{request.Value}");
}

public class InterfaceOnlySyncHandler : IRequestHandler<InterfaceOnlySyncRequest, string>
{
    public string Handle(InterfaceOnlySyncRequest request) => $"sync:{request.Value}";
}

public class InterfaceOnlyNotificationHandler : INotificationHandler<InterfaceOnlyNotification>
{
    public static List<string> Received { get; } = new();

    public Task Handle(InterfaceOnlyNotification notification, CancellationToken cancellationToken = default)
    {
        Received.Add(notification.Value);
        return Task.CompletedTask;
    }
}
