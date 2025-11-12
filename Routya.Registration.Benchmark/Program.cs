using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;
using System.Reflection;

namespace Routya.Registration.Benchmark;

internal class Program
{
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--validate")
        {
            RegistrationValidator.Validate();
            return;
        }
        
        // Run validator first to prove correctness
        Console.WriteLine("Running validation before benchmarks...\n");
        RegistrationValidator.Validate();
        Console.WriteLine("\nStarting benchmarks...\n");
        
        BenchmarkRunner.Run<RegistrationBenchmarks>();
    }
}

[MemoryDiagnoser]
public class RegistrationBenchmarks
{
    [Benchmark(Baseline = true)]
    public int AssemblyScanning_Registration()
    {
        var services = new ServiceCollection();
        
        // Register using assembly scanning
        services.AddRoutya(cfg => 
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        }, Assembly.GetExecutingAssembly());
        
        var provider = services.BuildServiceProvider();
        
        // Verify notification handlers were registered
        var registry = provider.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>();
        
        return registry.Count;
    }

    [Benchmark]
    public int ManualRegistration_Notification()
    {
        var services = new ServiceCollection();
        
        // Manually register notification handlers with different lifetimes
        services.AddRoutyaNotificationHandler<TestNotification, SingletonHandler>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<TestNotification, ScopedHandler>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<TestNotification, TransientHandler>(ServiceLifetime.Transient);
        
        // Register Routya core (without assembly scanning)
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        
        var provider = services.BuildServiceProvider();
        
        // Verify notification handlers were registered
        var registry = provider.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>();
        
        // Verify the handlers are in the registry
        if (registry.TryGetValue(typeof(INotificationHandler<TestNotification>), out var handlers))
        {
            // Should have 3 handlers: Singleton, Scoped, Transient
            return handlers.Count;
        }
        
        return 0;
    }

    [Benchmark]
    public int ManualRegistration_Mixed()
    {
        var services = new ServiceCollection();
        
        // Register request handlers
        services.AddRoutyaAsyncRequestHandler<TestRequest, string, TestAsyncHandler>(ServiceLifetime.Scoped);
        services.AddRoutyaRequestHandler<TestRequest, string, TestSyncHandler>(ServiceLifetime.Singleton);
        
        // Register notification handlers with different lifetimes
        services.AddRoutyaNotificationHandler<TestNotification, SingletonHandler>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<TestNotification, ScopedHandler>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<TestNotification, TransientHandler>(ServiceLifetime.Transient);
        
        // Register Routya core
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        
        var provider = services.BuildServiceProvider();
        
        // Verify notification handlers were registered
        var registry = provider.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>();
        
        int totalHandlers = 0;
        if (registry.TryGetValue(typeof(INotificationHandler<TestNotification>), out var notifHandlers))
        {
            totalHandlers += notifHandlers.Count;
        }
        
        // Also verify request handlers can be resolved
        var asyncHandler = provider.GetService<IAsyncRequestHandler<TestRequest, string>>();
        var syncHandler = provider.GetService<IRequestHandler<TestRequest, string>>();
        
        if (asyncHandler != null) totalHandlers++;
        if (syncHandler != null) totalHandlers++;
        
        return totalHandlers; // Should be 5 (3 notification + 2 request)
    }
}

// Test types - Shared between Program and RegistrationValidator
public record TestNotification(string Message) : INotification;
public record TestRequest(string Value) : IRequest<string>;

// Handlers for benchmarks
public class SingletonHandler : INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class ScopedHandler : INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public class TransientHandler : INotificationHandler<TestNotification>
{
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

// Handlers for validation
public class Handler1 : INotificationHandler<TestNotification>
{
    public static int ExecutedCount = 0;
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        ExecutedCount++;
        return Task.CompletedTask;
    }
}

public class Handler2 : INotificationHandler<TestNotification>
{
    public static int ExecutedCount = 0;
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        ExecutedCount++;
        return Task.CompletedTask;
    }
}

public class Handler3 : INotificationHandler<TestNotification>
{
    public static int ExecutedCount = 0;
    public Task Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        ExecutedCount++;
        return Task.CompletedTask;
    }
}

public class TestSyncHandler : IRequestHandler<TestRequest, string>
{
    public string Handle(TestRequest request) => $"Sync: {request.Value}";
}

public class TestAsyncHandler : IAsyncRequestHandler<TestRequest, string>
{
    public Task<string> HandleAsync(TestRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Async: {request.Value}");
    }
}
