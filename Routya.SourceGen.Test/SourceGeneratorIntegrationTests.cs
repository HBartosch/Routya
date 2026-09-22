using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Generated;
using System.Linq;
using System.Threading.Tasks;

namespace Routya.SourceGen.Test;

/// <summary>
/// Proves the source generator is working by exercising the generated
/// IGeneratedRoutya / AddGeneratedRoutya() code at runtime.
///
/// The generator discovers GetProductHandler, InventoryHandler, and AuditHandler
/// in this assembly at compile time and emits typed dispatch methods for them.
/// If the generator did not run, IGeneratedRoutya and AddGeneratedRoutya() would
/// not exist and this file would not compile.
/// </summary>
public class SourceGeneratorIntegrationTests
{
    // ── Compile-time proof ───────────────────────────────────────────────────
    // These tests do not need to run — the fact that this file compiles at all
    // proves the generator emitted IGeneratedRoutya and AddGeneratedRoutya().

    [Fact]
    public void IGeneratedRoutya_Type_Exists()
    {
        var type = typeof(IGeneratedRoutya);
        Assert.NotNull(type);
    }

    [Fact]
    public void AddGeneratedRoutya_Extension_Exists_And_Returns_Services()
    {
        var services = new ServiceCollection();
        var result = services.AddGeneratedRoutya();
        Assert.Same(services, result);
    }

    // ── DI registration ──────────────────────────────────────────────────────

    [Fact]
    public void AddGeneratedRoutya_Registers_IGeneratedRoutya()
    {
        var provider = BuildProvider();
        var routya = provider.GetService<IGeneratedRoutya>();
        Assert.NotNull(routya);
    }

    [Fact]
    public void AddGeneratedRoutya_Registers_Request_Handler()
    {
        var provider = BuildProvider();
        var handler = provider.GetService<IAsyncRequestHandler<GetProductRequest, Product>>();
        Assert.NotNull(handler);
        Assert.IsType<GetProductHandler>(handler);
    }

    [Fact]
    public void AddGeneratedRoutya_Registers_Both_Notification_Handlers()
    {
        var provider = BuildProvider();
        var handlers = provider.GetServices<INotificationHandler<ProductCreatedNotification>>().ToList();
        Assert.Equal(2, handlers.Count);
        Assert.Contains(handlers, h => h is InventoryHandler);
        Assert.Contains(handlers, h => h is AuditHandler);
    }

    // ── Dispatch correctness ─────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Dispatches_To_Generated_Handler_And_Returns_Correct_Response()
    {
        var routya = BuildProvider().GetRequiredService<IGeneratedRoutya>();
        var result = await routya.SendAsync(new GetProductRequest(42));
        Assert.Equal(42, result.Id);
        Assert.Equal("Product_42", result.Name);
    }

    [Fact]
    public async Task SendAsync_Different_Ids_Return_Distinct_Responses()
    {
        var routya = BuildProvider().GetRequiredService<IGeneratedRoutya>();
        var a = await routya.SendAsync(new GetProductRequest(1));
        var b = await routya.SendAsync(new GetProductRequest(2));
        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(a.Name, b.Name);
    }

    [Fact]
    public async Task PublishAsync_Invokes_All_Notification_Handlers()
    {
        var tracker = new HandlerCallTracker();
        var routya = BuildProvider(tracker).GetRequiredService<IGeneratedRoutya>();

        await routya.PublishAsync(new ProductCreatedNotification { ProductId = 7 });

        Assert.Contains(nameof(InventoryHandler), tracker.Calls);
        Assert.Contains(nameof(AuditHandler), tracker.Calls);
    }

    [Fact]
    public async Task PublishAsync_Invokes_Each_Handler_Exactly_Once()
    {
        var tracker = new HandlerCallTracker();
        var routya = BuildProvider(tracker).GetRequiredService<IGeneratedRoutya>();

        await routya.PublishAsync(new ProductCreatedNotification { ProductId = 1 });

        Assert.Equal(1, tracker.Calls.Count(c => c == nameof(InventoryHandler)));
        Assert.Equal(1, tracker.Calls.Count(c => c == nameof(AuditHandler)));
    }

    // ── Synchronous dispatch ─────────────────────────────────────────────────

    [Fact]
    public void AddGeneratedRoutya_Registers_Sync_Request_Handler()
    {
        var provider = BuildProvider();
        var handler = provider.GetService<IRequestHandler<CalculateTotalRequest, decimal>>();
        Assert.NotNull(handler);
        Assert.IsType<CalculateTotalHandler>(handler);
    }

    [Fact]
    public void Send_Dispatches_To_Sync_Handler_And_Returns_Correct_Response()
    {
        var routya = BuildProvider().GetRequiredService<IGeneratedRoutya>();

        var result = routya.Send(new CalculateTotalRequest(3, 9.99m));

        Assert.Equal(29.97m, result);
    }

    [Fact]
    public void Send_Runs_Pipeline_Behaviors_Around_A_Sync_Handler()
    {
        var tracker = new HandlerCallTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TrackingBehavior<,>));
        services.AddGeneratedRoutya();
        var routya = services.BuildServiceProvider().GetRequiredService<IGeneratedRoutya>();

        var result = routya.Send(new CalculateTotalRequest(2, 5m));

        Assert.Equal(10m, result);
        Assert.NotEmpty(tracker.Calls);
    }

    // ── Fan out when a handler faults ────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_Waits_For_Every_Handler_Even_When_One_Faults()
    {
        var routya = BuildProvider().GetRequiredService<IGeneratedRoutya>();
        FanOutSlowHandler.Completed = false;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => routya.PublishAsync(new FanOutFailureNotification()));

        Assert.True(
            FanOutSlowHandler.Completed,
            "A faulting handler must not let the publish return while another handler is still running.");
    }

    // ── Internal handlers ────────────────────────────────────────────────────

    [Fact]
    public void AddGeneratedRoutya_Registers_An_Internal_Handler()
    {
        var provider = BuildProvider();
        var handler = provider.GetService<IAsyncRequestHandler<ArchiveOrderCommand, bool>>();
        Assert.NotNull(handler);
    }

    [Fact]
    public async Task SendAsync_Dispatches_To_An_Internal_Handler()
    {
        var routya = BuildProvider().GetRequiredService<IGeneratedRoutya>();

        Assert.True(await routya.SendAsync(new ArchiveOrderCommand(1)));
        Assert.False(await routya.SendAsync(new ArchiveOrderCommand(0)));
    }

    // ── Pipeline behavior ────────────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_Behavior_Executes_Before_Handler()
    {
        var tracker = new HandlerCallTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TrackingBehavior<,>));
        services.AddGeneratedRoutya();
        var routya = services.BuildServiceProvider().GetRequiredService<IGeneratedRoutya>();

        await routya.SendAsync(new GetProductRequest(1));

        Assert.Contains(tracker.Calls, c => c.StartsWith(nameof(TrackingBehavior<GetProductRequest, Product>)[..15]));
    }

    [Fact]
    public async Task Pipeline_Behavior_Does_Not_Affect_Response_Value()
    {
        var tracker = new HandlerCallTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TrackingBehavior<,>));
        services.AddGeneratedRoutya();
        var routya = services.BuildServiceProvider().GetRequiredService<IGeneratedRoutya>();

        var result = await routya.SendAsync(new GetProductRequest(99));

        Assert.Equal(99, result.Id);
        Assert.Equal("Product_99", result.Name);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IServiceProvider BuildProvider(HandlerCallTracker? tracker = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tracker ?? new HandlerCallTracker());
        services.AddGeneratedRoutya();
        return services.BuildServiceProvider();
    }
}
