using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Covers first class streaming through <see cref="IRoutya.CreateStream{TRequest, TResponse}"/>.
/// </summary>
public class StreamRequestTests
{
    [Fact]
    public async Task CreateStream_Yields_All_Items_In_Order()
    {
        var routya = BuildProvider().GetRequiredService<IRoutya>();

        var items = new List<int>();
        await foreach (var item in routya.CreateStream<CountTo, int>(new CountTo(4)))
        {
            items.Add(item);
        }

        Assert.Equal(new[] { 1, 2, 3, 4 }, items);
    }

    [Fact]
    public async Task CreateStream_Is_Lazy_And_Does_Not_Run_Until_Enumerated()
    {
        var routya = BuildProvider().GetRequiredService<IRoutya>();
        CountToHandler.ItemsProduced = 0;

        // Obtaining the sequence must not produce anything
        var stream = routya.CreateStream<CountTo, int>(new CountTo(3));
        Assert.Equal(0, CountToHandler.ItemsProduced);

        await foreach (var _ in stream)
        {
        }

        Assert.Equal(3, CountToHandler.ItemsProduced);
    }

    [Fact]
    public async Task CreateStream_Does_Not_Buffer_So_Items_Arrive_As_Produced()
    {
        var routya = BuildProvider().GetRequiredService<IRoutya>();
        CountToHandler.ItemsProduced = 0;

        var seen = 0;
        await foreach (var _ in routya.CreateStream<CountTo, int>(new CountTo(5)))
        {
            seen++;

            // The handler must not have run ahead of the consumer
            Assert.Equal(seen, CountToHandler.ItemsProduced);
        }

        Assert.Equal(5, seen);
    }

    [Fact]
    public async Task Stream_Behaviors_Observe_Every_Item()
    {
        // The whole point of IStreamRequest over IRequest<IAsyncEnumerable<T>>: behaviors wrap the
        // enumeration rather than the handover, so they see each item.
        var services = BuildServices();
        services.AddTransient(typeof(IStreamPipelineBehavior<,>), typeof(CountingStreamBehavior<,>));

        var routya = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .GetRequiredService<IRoutya>();

        StreamBehaviorTracker.ItemsSeen = 0;
        StreamBehaviorTracker.Completed = false;

        await foreach (var _ in routya.CreateStream<CountTo, int>(new CountTo(4)))
        {
        }

        Assert.Equal(4, StreamBehaviorTracker.ItemsSeen);
        Assert.True(StreamBehaviorTracker.Completed, "The behavior should have observed the end of the stream.");
    }

    [Fact]
    public async Task Stream_Behaviors_Observe_An_Exception_Thrown_Part_Way_Through()
    {
        // With IRequest<IAsyncEnumerable<T>> the pipeline has already completed by this point, so
        // a behavior could never see this failure.
        var services = BuildServices();
        services.AddRoutyaStreamRequestHandler<FailMidway, int, FailMidwayHandler>(ServiceLifetime.Scoped);
        services.AddTransient(typeof(IStreamPipelineBehavior<,>), typeof(CountingStreamBehavior<,>));

        var routya = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .GetRequiredService<IRoutya>();

        StreamBehaviorTracker.ItemsSeen = 0;
        StreamBehaviorTracker.SawException = false;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in routya.CreateStream<FailMidway, int>(new FailMidway()))
            {
            }
        });

        Assert.Equal(2, StreamBehaviorTracker.ItemsSeen);
        Assert.True(StreamBehaviorTracker.SawException, "The behavior should have observed the failure.");
    }

    [Fact]
    public async Task Stream_Behaviors_Run_In_Registration_Order()
    {
        var services = BuildServices();
        services.AddTransient<IStreamPipelineBehavior<CountTo, int>, FirstOrderBehavior>();
        services.AddTransient<IStreamPipelineBehavior<CountTo, int>, SecondOrderBehavior>();

        var routya = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .GetRequiredService<IRoutya>();

        StreamOrderTracker.Order.Clear();

        await foreach (var _ in routya.CreateStream<CountTo, int>(new CountTo(1)))
        {
        }

        Assert.Equal(new[] { "first", "second" }, StreamOrderTracker.Order);
    }

    [Fact]
    public async Task CreateStream_Keeps_The_Dispatch_Scope_Alive_For_The_Whole_Enumeration()
    {
        var services = new ServiceCollection();
        services.AddScoped<StreamScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaStreamRequestHandler<ScopeProbe, Guid, ScopeProbeHandler>(ServiceLifetime.Scoped);

        var routya = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .GetRequiredService<IRoutya>();

        var observed = new List<Guid>();
        await foreach (var id in routya.CreateStream<ScopeProbe, Guid>(new ScopeProbe(3)))
        {
            observed.Add(id);
        }

        // All three items came from the same scoped dependency, so the scope survived the stream
        Assert.Equal(3, observed.Count);
        Assert.Single(observed.Distinct());
    }

    [Fact]
    public async Task Each_CreateStream_Gets_Its_Own_Scope()
    {
        var services = new ServiceCollection();
        services.AddScoped<StreamScopedDependency>();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaStreamRequestHandler<ScopeProbe, Guid, ScopeProbeHandler>(ServiceLifetime.Scoped);

        var routya = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true })
            .GetRequiredService<IRoutya>();

        var first = await FirstItem(routya);
        var second = await FirstItem(routya);

        Assert.NotEqual(first, second);

        static async Task<Guid> FirstItem(IRoutya routya)
        {
            await foreach (var id in routya.CreateStream<ScopeProbe, Guid>(new ScopeProbe(1)))
            {
                return id;
            }

            throw new InvalidOperationException("stream produced nothing");
        }
    }

    [Fact]
    public async Task CreateStream_Can_Be_Cancelled_Part_Way_Through()
    {
        var routya = BuildProvider().GetRequiredService<IRoutya>();
        using var cts = new CancellationTokenSource();

        var received = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in routya.CreateStream<CountTo, int>(new CountTo(100), cts.Token))
            {
                received++;
                if (received == 3)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(3, received);
    }

    [Fact]
    public async Task CreateStream_Throws_When_No_Handler_Is_Registered()
    {
        var services = new ServiceCollection();
        services.AddRoutya();
        var routya = services.BuildServiceProvider().GetRequiredService<IRoutya>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in routya.CreateStream<Unhandled, int>(new Unhandled()))
            {
            }
        });

        Assert.Contains("No stream handler found", exception.Message);
    }

    private static IServiceProvider BuildProvider()
        => BuildServices().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddRoutya(cfg => cfg.Scope = RoutyaDispatchScope.Scoped);
        services.AddRoutyaStreamRequestHandler<CountTo, int, CountToHandler>(ServiceLifetime.Scoped);
        return services;
    }
}

// Test models
public record CountTo(int Count) : IStreamRequest<int>;
public record FailMidway : IStreamRequest<int>;
public record ScopeProbe(int Count) : IStreamRequest<Guid>;
public record Unhandled : IStreamRequest<int>;

public class StreamScopedDependency
{
    public Guid Id { get; } = Guid.NewGuid();
}

public class CountToHandler : IStreamRequestHandler<CountTo, int>
{
    public static int ItemsProduced { get; set; }

    public async IAsyncEnumerable<int> Handle(
        CountTo request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 1; i <= request.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            ItemsProduced++;
            yield return i;
        }
    }
}

public class FailMidwayHandler : IStreamRequestHandler<FailMidway, int>
{
    public async IAsyncEnumerable<int> Handle(
        FailMidway request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return 1;
        yield return 2;
        throw new InvalidOperationException("stream failed part way through");
    }
}

public class ScopeProbeHandler : IStreamRequestHandler<ScopeProbe, Guid>
{
    private readonly StreamScopedDependency _dependency;

    public ScopeProbeHandler(StreamScopedDependency dependency) => _dependency = dependency;

    public async IAsyncEnumerable<Guid> Handle(
        ScopeProbe request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = 0; i < request.Count; i++)
        {
            await Task.Yield();

            // Reading the scoped dependency on every item proves the scope is still alive
            yield return _dependency.Id;
        }
    }
}

public static class StreamBehaviorTracker
{
    public static int ItemsSeen { get; set; }
    public static bool Completed { get; set; }
    public static bool SawException { get; set; }
}

public class CountingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        TRequest request,
        StreamHandlerDelegate<TResponse> next,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var enumerator = next(cancellationToken).GetAsyncEnumerator(cancellationToken);

        try
        {
            while (true)
            {
                TResponse current;

                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                    {
                        break;
                    }

                    current = enumerator.Current;
                }
                catch (InvalidOperationException)
                {
                    StreamBehaviorTracker.SawException = true;
                    throw;
                }

                StreamBehaviorTracker.ItemsSeen++;
                yield return current;
            }

            StreamBehaviorTracker.Completed = true;
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}

public static class StreamOrderTracker
{
    public static List<string> Order { get; } = new();
}

public class FirstOrderBehavior : IStreamPipelineBehavior<CountTo, int>
{
    public IAsyncEnumerable<int> Handle(
        CountTo request,
        StreamHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        StreamOrderTracker.Order.Add("first");
        return next(cancellationToken);
    }
}

public class SecondOrderBehavior : IStreamPipelineBehavior<CountTo, int>
{
    public IAsyncEnumerable<int> Handle(
        CountTo request,
        StreamHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        StreamOrderTracker.Order.Add("second");
        return next(cancellationToken);
    }
}
