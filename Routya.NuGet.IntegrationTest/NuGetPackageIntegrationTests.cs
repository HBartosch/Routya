using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Generated;
using System.Linq;
using System.Threading.Tasks;

namespace Routya.NuGet.IntegrationTest;

/// <summary>
/// Proves the 'Routya' meta-package works end-to-end when installed as a NuGet package.
///
/// This project references only:
///     <PackageReference Include="Routya" Version="..." />
///
/// The source generator (Routya.SourceGenerators) is a transitive dependency of
/// the meta-package and is picked up automatically from its analyzers/ folder.
/// If the generator did not run, IGeneratedRoutya and AddGeneratedRoutya() would
/// not exist and this file would not compile.
///
/// Generated files are written to obj/Generated/ (EmitCompilerGeneratedFiles=true)
/// so you can inspect RoutyaGenerated.Registration.g.cs and RoutyaGenerated.Dispatcher.g.cs.
/// </summary>
public class NuGetPackageIntegrationTests
{
    // ── Compile-time proof ────────────────────────────────────────────────────
    // These tests prove the generator ran via the NuGet meta-package.
    // The mere fact this file compiles means IGeneratedRoutya was generated.

    [Fact]
    public void Source_Generator_Ran_IGeneratedRoutya_Exists()
    {
        var type = typeof(IGeneratedRoutya);
        Assert.NotNull(type);
    }

    [Fact]
    public void Source_Generator_Ran_AddGeneratedRoutya_Exists()
    {
        var services = new ServiceCollection();
        var result = services.AddGeneratedRoutya();
        Assert.Same(services, result);
    }

    // ── DI registration ───────────────────────────────────────────────────────

    [Fact]
    public void AddGeneratedRoutya_Resolves_IGeneratedRoutya()
    {
        var routya = BuildProvider().GetService<IGeneratedRoutya>();
        Assert.NotNull(routya);
    }

    [Fact]
    public void AddGeneratedRoutya_Registers_Request_Handler()
    {
        var handler = BuildProvider().GetService<IAsyncRequestHandler<GetOrderRequest, Order>>();
        Assert.NotNull(handler);
        Assert.IsType<GetOrderHandler>(handler);
    }

    [Fact]
    public void AddGeneratedRoutya_Registers_Both_Notification_Handlers()
    {
        var handlers = BuildProvider()
            .GetServices<INotificationHandler<OrderShippedEvent>>()
            .ToList();
        Assert.Equal(2, handlers.Count);
        Assert.Contains(handlers, h => h is EmailNotificationHandler);
        Assert.Contains(handlers, h => h is WarehouseHandler);
    }

    // ── Dispatch ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Returns_Correct_Response()
    {
        var routya = BuildProvider().GetRequiredService<IGeneratedRoutya>();
        var order = await routya.SendAsync(new GetOrderRequest(42));
        Assert.Equal(42, order.Id);
        Assert.Equal("Confirmed_42", order.Status);
    }

    [Fact]
    public async Task PublishAsync_Invokes_All_Handlers()
    {
        var tracker = new InvocationTracker();
        var routya = BuildProvider(tracker).GetRequiredService<IGeneratedRoutya>();

        await routya.PublishAsync(new OrderShippedEvent { OrderId = 7 });

        Assert.Contains(nameof(EmailNotificationHandler), tracker.Calls);
        Assert.Contains(nameof(WarehouseHandler), tracker.Calls);
        Assert.Equal(2, tracker.Calls.Count);
    }

    // ── Pipeline behavior ─────────────────────────────────────────────────────

    [Fact]
    public async Task Pipeline_Behavior_Executes_And_Result_Is_Correct()
    {
        var tracker = new InvocationTracker();
        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RecordingBehavior<,>));
        services.AddGeneratedRoutya();
        var routya = services.BuildServiceProvider().GetRequiredService<IGeneratedRoutya>();

        var order = await routya.SendAsync(new GetOrderRequest(99));

        Assert.Equal(99, order.Id);
        Assert.Contains(tracker.Calls, c => c.StartsWith(nameof(RecordingBehavior<GetOrderRequest, Order>)[..17]));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static IServiceProvider BuildProvider(InvocationTracker? tracker = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(tracker ?? new InvocationTracker());
        services.AddGeneratedRoutya();
        return services.BuildServiceProvider();
    }
}
