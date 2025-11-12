using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Registration.Benchmark;

/// <summary>
/// Validates that manual registration correctly populates the handler registry
/// </summary>
public class RegistrationValidator
{
    public static void Validate()
    {
        Console.WriteLine("=== Routya Manual Registration Validator ===\n");
        
        ValidateNotificationHandlerRegistry();
        ValidateHandlerLifetimes();
        ValidateMultipleHandlersPerNotification();
        ValidateMixedRegistration();
        
        Console.WriteLine("\n✅ All validations passed!");
    }

    static void ValidateNotificationHandlerRegistry()
    {
        Console.WriteLine("[1] Validating Notification Handler Registry...");
        
        var services = new ServiceCollection();
        
        // Manually register handlers (registry is created automatically by AddRoutyaNotificationHandler)
        services.AddRoutyaNotificationHandler<TestNotification, Handler1>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<TestNotification, Handler2>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<TestNotification, Handler3>(ServiceLifetime.Transient);
        
        // AddRoutya must be called AFTER manual registrations to finalize setup
        services.AddRoutya();
        var provider = services.BuildServiceProvider();
        
        // Get the registry
        var registry = provider.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>();
        
        Console.WriteLine($"   Registry contains {registry.Count} entries:");
        foreach (var entry in registry)
        {
            Console.WriteLine($"     - {entry.Key.Name}: {entry.Value.Count} handlers");
        }
        
        // Validate
        var handlerInterface = typeof(INotificationHandler<TestNotification>);
        if (!registry.ContainsKey(handlerInterface))
        {
            throw new Exception($"❌ Handler interface not found in registry! Looking for: {handlerInterface.Name}");
        }
        
        var handlers = registry[handlerInterface];
        if (handlers.Count != 3)
        {
            throw new Exception($"❌ Expected 3 handlers, found {handlers.Count}");
        }
        
        Console.WriteLine($"   ✓ Found {handlers.Count} handlers in registry");
        Console.WriteLine($"   ✓ Registry contains: {handlerInterface.Name}");
    }

    static void ValidateHandlerLifetimes()
    {
        Console.WriteLine("\n[2] Validating Handler Lifetimes...");
        
        var services = new ServiceCollection();
        
        services.AddRoutyaNotificationHandler<TestNotification, Handler1>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<TestNotification, Handler2>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<TestNotification, Handler3>(ServiceLifetime.Transient);
        
        services.AddRoutya();
        var provider = services.BuildServiceProvider();
        
        var registry = provider.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>();
        var handlers = registry[typeof(INotificationHandler<TestNotification>)];
        
        // Verify lifetimes
        var singletonHandler = handlers.FirstOrDefault(h => h.ConcreteType == typeof(Handler1));
        var scopedHandler = handlers.FirstOrDefault(h => h.ConcreteType == typeof(Handler2));
        var transientHandler = handlers.FirstOrDefault(h => h.ConcreteType == typeof(Handler3));
        
        if (singletonHandler?.Lifetime != ServiceLifetime.Singleton)
            throw new Exception("❌ Handler1 should be Singleton!");
        
        if (scopedHandler?.Lifetime != ServiceLifetime.Scoped)
            throw new Exception("❌ Handler2 should be Scoped!");
        
        if (transientHandler?.Lifetime != ServiceLifetime.Transient)
            throw new Exception("❌ Handler3 should be Transient!");
        
        Console.WriteLine("   ✓ Singleton handler registered correctly");
        Console.WriteLine("   ✓ Scoped handler registered correctly");
        Console.WriteLine("   ✓ Transient handler registered correctly");
    }

    static void ValidateMultipleHandlersPerNotification()
    {
        Console.WriteLine("\n[3] Validating Multiple Handlers Per Notification...");
        
        var services = new ServiceCollection();
        
        // Register 3 handlers for the same notification
        services.AddRoutyaNotificationHandler<TestNotification, Handler1>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<TestNotification, Handler2>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<TestNotification, Handler3>(ServiceLifetime.Transient);
        
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        var provider = services.BuildServiceProvider();
        
        // Execute notification
        using var scope = provider.CreateScope();
        var routya = scope.ServiceProvider.GetRequiredService<IRoutya>();
        
        Handler1.ExecutedCount = 0;
        Handler2.ExecutedCount = 0;
        Handler3.ExecutedCount = 0;
        
        routya.PublishAsync(new TestNotification("test")).Wait();
        
        if (Handler1.ExecutedCount != 1)
            throw new Exception($"❌ Handler1 executed {Handler1.ExecutedCount} times, expected 1");
        
        if (Handler2.ExecutedCount != 1)
            throw new Exception($"❌ Handler2 executed {Handler2.ExecutedCount} times, expected 1");
        
        if (Handler3.ExecutedCount != 1)
            throw new Exception($"❌ Handler3 executed {Handler3.ExecutedCount} times, expected 1");
        
        Console.WriteLine("   ✓ All 3 handlers executed");
        Console.WriteLine($"   ✓ Handler1 (Singleton): {Handler1.ExecutedCount}x");
        Console.WriteLine($"   ✓ Handler2 (Scoped): {Handler2.ExecutedCount}x");
        Console.WriteLine($"   ✓ Handler3 (Transient): {Handler3.ExecutedCount}x");
    }

    static void ValidateMixedRegistration()
    {
        Console.WriteLine("\n[4] Validating Mixed Request & Notification Registration...");
        
        var services = new ServiceCollection();
        
        // Register request handlers
        services.AddRoutyaAsyncRequestHandler<TestRequest, string, TestAsyncHandler>(ServiceLifetime.Scoped);
        services.AddRoutyaRequestHandler<TestRequest, string, TestSyncHandler>(ServiceLifetime.Singleton);
        
        // Register notification handlers
        services.AddRoutyaNotificationHandler<TestNotification, Handler1>(ServiceLifetime.Singleton);
        
        services.AddRoutya();
        var provider = services.BuildServiceProvider();
        
        // Verify request handlers
        var asyncHandler = provider.GetService<IAsyncRequestHandler<TestRequest, string>>();
        var syncHandler = provider.GetService<IRequestHandler<TestRequest, string>>();
        
        if (asyncHandler == null)
            throw new Exception("❌ Async request handler not registered!");
        
        if (syncHandler == null)
            throw new Exception("❌ Sync request handler not registered!");
        
        // Verify notification handlers
        var registry = provider.GetRequiredService<Dictionary<Type, List<NotificationHandlerInfo>>>();
        if (!registry.ContainsKey(typeof(INotificationHandler<TestNotification>)))
            throw new Exception("❌ Notification handler not in registry!");
        
        Console.WriteLine("   ✓ Async request handler registered");
        Console.WriteLine("   ✓ Sync request handler registered");
        Console.WriteLine("   ✓ Notification handler registered");
    }
}


