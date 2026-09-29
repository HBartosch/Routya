using System.IO;
﻿using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Extensions;

namespace Routya.Test.CoreTests;

/// <summary>
/// Guards the per dispatch allocation cost of the runtime dispatcher.
/// </summary>
/// <remarks>
/// <para>
/// Allocated bytes are deterministic: they do not depend on CPU, machine load or GC mode, which
/// makes them the one performance characteristic that can be asserted reliably on a shared CI
/// runner. Timings cannot. The MediatR baseline in the benchmarks here has been observed drifting
/// by a third between runs on identical code.
/// </para>
/// <para>
/// The budgets below were measured on net8.0 and carry roughly 25% headroom, so ordinary variation
/// between runtime patch versions will not fail the build but a real regression will. If one of
/// these fails, find out what started allocating before raising the budget. Raising it should be a
/// deliberate decision recorded with the new measured figure, not a nudge to make CI green.
/// </para>
/// </remarks>
public class AllocationBudgetTests
{
    private readonly ITestOutputHelper _output;

    public AllocationBudgetTests(ITestOutputHelper output) => _output = output;

    // Measured on net8.0, then rounded up for headroom.
    private const long RootSingletonSendBudget = 128;          // measured 104 B
    private const long RootSingletonSendAsyncBudget = 256;     // measured 208 B
    private const long RootSingletonPublishBudget = 64;        // measured  32 B
    private const long ScopedSendAsyncBudget = 640;            // measured 544 B
    private const long ScopedPublishBudget = 480;              // measured 392 B
    private const long ScopedParallelPublishBudget = 1024;     // measured 824 B, one scope per handler

    [Fact]
    public void Send_With_Singleton_Handler_Stays_Within_Budget()
    {
        var routya = BuildRootSingletonProvider().GetRequiredService<IRoutya>();
        var request = new AllocSyncPing("x");

        var bytes = AllocationProbe.PerOperation(() => routya.Send<AllocSyncPing, string>(request));

        AssertWithinBudget(bytes, RootSingletonSendBudget, "Send, Singleton handler, dispatch only");
    }

    [Fact]
    public void SendAsync_With_Singleton_Handler_Stays_Within_Budget()
    {
        var routya = BuildRootSingletonProvider().GetRequiredService<IRoutya>();
        var request = new AllocPing("x");

        var bytes = AllocationProbe.PerOperation(
            () => routya.SendAsync<AllocPing, string>(request));

        AssertWithinBudget(bytes, RootSingletonSendAsyncBudget, "SendAsync, Singleton handler, dispatch only");
    }

    [Fact]
    public void PublishAsync_With_Two_Singleton_Handlers_Stays_Within_Budget()
    {
        var routya = BuildRootSingletonProvider().GetRequiredService<IRoutya>();
        var notification = new AllocEvent(1);

        var bytes = AllocationProbe.PerOperation(
            () => routya.PublishAsync(notification));

        AssertWithinBudget(bytes, RootSingletonPublishBudget, "PublishAsync, 2 Singleton handlers, dispatch only");
    }

    [Fact]
    public void SendAsync_With_Scoped_Handler_Stays_Within_Budget()
    {
        var routya = BuildScopedProvider().GetRequiredService<IRoutya>();
        var request = new AllocPing("x");

        var bytes = AllocationProbe.PerOperation(
            () => routya.SendAsync<AllocPing, string>(request));

        AssertWithinBudget(bytes, ScopedSendAsyncBudget, "SendAsync, Scoped handler, includes dispatch scope");
    }

    [Fact]
    public void PublishAsync_With_Two_Scoped_Handlers_Stays_Within_Budget()
    {
        var routya = BuildScopedProvider().GetRequiredService<IRoutya>();
        var notification = new AllocEvent(1);

        var bytes = AllocationProbe.PerOperation(
            () => routya.PublishAsync(notification));

        AssertWithinBudget(bytes, ScopedPublishBudget, "PublishAsync, 2 Scoped handlers, includes dispatch scope");
    }

    [Fact]
    public void PublishParallelAsync_With_Two_Scoped_Handlers_Stays_Within_Budget()
    {
        var routya = BuildScopedProvider().GetRequiredService<IRoutya>();
        var notification = new AllocEvent(1);

        var bytes = AllocationProbe.PerOperation(
            () => routya.PublishParallelAsync(notification));

        AssertWithinBudget(bytes, ScopedParallelPublishBudget, "PublishParallelAsync, 2 Scoped handlers, scope per handler");
    }

    private void AssertWithinBudget(long measured, long budget, string what)
    {
        AllocationReport.Record(_output, "Routya.Core runtime dispatch", what, measured, budget);

        Assert.True(
            measured <= budget,
            $"{what}: allocated {measured} B per operation, budget is {budget} B. "
            + "Something started allocating. Find out what before changing the budget.");
    }

    private static ServiceProvider BuildRootSingletonProvider()
    {
        var services = new ServiceCollection();
        services.AddRoutya(cfg =>
        {
            cfg.Scope = RoutyaDispatchScope.Root;
            cfg.HandlerLifetime = ServiceLifetime.Singleton;
        });
        services.AddRoutyaRequestHandler<AllocSyncPing, string, AllocSyncPingHandler>(ServiceLifetime.Singleton);
        services.AddRoutyaAsyncRequestHandler<AllocPing, string, AllocPingHandler>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<AllocEvent, AllocEventHandlerOne>(ServiceLifetime.Singleton);
        services.AddRoutyaNotificationHandler<AllocEvent, AllocEventHandlerTwo>(ServiceLifetime.Singleton);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildScopedProvider()
    {
        var services = new ServiceCollection();
        services.AddRoutya(cfg =>
        {
            cfg.Scope = RoutyaDispatchScope.Scoped;
            cfg.HandlerLifetime = ServiceLifetime.Scoped;
        });
        services.AddRoutyaAsyncRequestHandler<AllocPing, string, AllocPingHandler>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<AllocEvent, AllocEventHandlerOne>(ServiceLifetime.Scoped);
        services.AddRoutyaNotificationHandler<AllocEvent, AllocEventHandlerTwo>(ServiceLifetime.Scoped);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}

/// <summary>
/// Measures the managed bytes a single operation allocates.
/// </summary>
/// <remarks>
/// Uses the per thread allocation counter, so tests running in parallel on other threads cannot
/// interfere. The measured operation must complete synchronously, otherwise its continuation runs
/// on a thread pool thread and those allocations are not counted here.
/// </remarks>
public static class AllocationProbe
{
    public static long PerOperation(Action operation, int warmup = 500, int iterations = 2000)
    {
        // The first calls build and cache the dispatch delegate, so they must not be measured
        for (var i = 0; i < warmup; i++)
        {
            operation();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < iterations; i++)
        {
            operation();
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / iterations;
    }

    /// <summary>
    /// Overload for operations that return a task.
    /// </summary>
    /// <remarks>
    /// The blocking wait lives here rather than in the test method deliberately. The measured
    /// operation has to complete synchronously on this thread, because
    /// GetAllocatedBytesForCurrentThread counts per thread and a continuation resumed on the thread
    /// pool would not be counted. Every handler used by these tests completes synchronously, so the
    /// task is already finished and nothing actually blocks.
    /// </remarks>
    public static long PerOperation(Func<Task> operation, int warmup = 500, int iterations = 2000)
        => PerOperation(() => operation().GetAwaiter().GetResult(), warmup, iterations);
}

// Test models, named distinctly so they do not share dispatch caches with other test classes
public record AllocPing(string Name) : IRequest<string>;
public record AllocSyncPing(string Name) : IRequest<string>;
public record AllocEvent(int Id) : INotification;

public class AllocPingHandler : IAsyncRequestHandler<AllocPing, string>
{
    public Task<string> HandleAsync(AllocPing request, CancellationToken cancellationToken)
        => Task.FromResult(request.Name);
}

public class AllocSyncPingHandler : IRequestHandler<AllocSyncPing, string>
{
    public string Handle(AllocSyncPing request) => request.Name;
}

public class AllocEventHandlerOne : INotificationHandler<AllocEvent>
{
    public Task Handle(AllocEvent notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class AllocEventHandlerTwo : INotificationHandler<AllocEvent>
{
    public Task Handle(AllocEvent notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Reports a measured allocation figure so it is visible on a green run, not only on failure.
/// </summary>
/// <remarks>
/// Writes to the test output, and when running under GitHub Actions also appends a row to the job
/// summary, which renders as a table on the workflow run page. Without this the budgets pass
/// silently and a pipeline tells you nothing about what the code actually allocates on that
/// hardware.
/// </remarks>
public static class AllocationReport
{
    private static readonly object Gate = new object();
    private static bool _headerWritten;

    public static void Record(ITestOutputHelper output, string suite, string scenario, long measured, long budget)
    {
        var headroom = budget - measured;
        output.WriteLine($"{scenario}: {measured} B per operation (budget {budget} B, {headroom} B headroom)");

        var summaryPath = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

        if (string.IsNullOrEmpty(summaryPath))
        {
            return;
        }

        lock (Gate)
        {
            if (!_headerWritten)
            {
                File.AppendAllText(
                    summaryPath,
                    $"{Environment.NewLine}### Allocation budgets: {suite}{Environment.NewLine}{Environment.NewLine}"
                    + $"| Scenario | Measured | Budget | Headroom |{Environment.NewLine}"
                    + $"|---|---:|---:|---:|{Environment.NewLine}");
                _headerWritten = true;
            }

            File.AppendAllText(
                summaryPath,
                $"| {scenario} | {measured} B | {budget} B | {headroom} B |{Environment.NewLine}");
        }
    }
}
