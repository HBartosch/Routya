using Xunit.Abstractions;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Generated;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.SourceGen.Test;

/// <summary>
/// Guards the per dispatch allocation cost of the source generated dispatcher, which is the path
/// most consumers use.
/// </summary>
/// <remarks>
/// See the equivalent tests in Routya.Test for why allocations rather than timings are asserted.
/// Budgets were measured on net8.0 and carry roughly 25% headroom. If one fails, find out what
/// started allocating before raising it.
/// </remarks>
public class GeneratedAllocationBudgetTests
{
    private readonly ITestOutputHelper _output;

    public GeneratedAllocationBudgetTests(ITestOutputHelper output) => _output = output;

    // Measured on net8.0, then rounded up for headroom.
    private const long SendAsyncBudget = 256;   // measured 200 B
    private const long SendBudget = 96;         // measured  56 B
    private const long PublishAsyncBudget = 224; // measured 168 B

    [Fact]
    public void SendAsync_Stays_Within_Budget()
    {
        var routya = BuildProvider();
        var request = new AllocGenPing("x");

        var bytes = GeneratedAllocationProbe.PerOperation(
            () => routya.SendAsync(request));

        AssertWithinBudget(bytes, SendAsyncBudget, "Generated SendAsync");
    }

    [Fact]
    public void Send_Stays_Within_Budget()
    {
        var routya = BuildProvider();
        var request = new AllocGenSyncPing("x");

        var bytes = GeneratedAllocationProbe.PerOperation(() => routya.Send(request));

        AssertWithinBudget(bytes, SendBudget, "Generated Send");
    }

    [Fact]
    public void PublishAsync_With_Two_Handlers_Stays_Within_Budget()
    {
        var routya = BuildProvider();
        var notification = new AllocGenEvent(1);

        var bytes = GeneratedAllocationProbe.PerOperation(
            () => routya.PublishAsync(notification));

        AssertWithinBudget(bytes, PublishAsyncBudget, "Generated PublishAsync with two handlers");
    }

    private void AssertWithinBudget(long measured, long budget, string what)
    {
        GeneratedAllocationReport.Record(_output, "Routya source generated dispatch", what, measured, budget);

        Assert.True(
            measured <= budget,
            $"{what}: allocated {measured} B per operation, budget is {budget} B. "
            + "Something started allocating. Find out what before changing the budget.");
    }

    private static IGeneratedRoutya BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HandlerCallTracker());
        services.AddGeneratedRoutya();
        return services.BuildServiceProvider().GetRequiredService<IGeneratedRoutya>();
    }
}

/// <summary>
/// Measures the managed bytes a single operation allocates, using the per thread counter so that
/// tests running in parallel on other threads cannot interfere.
/// </summary>
public static class GeneratedAllocationProbe
{
    public static long PerOperation(Action operation, int warmup = 500, int iterations = 2000)
    {
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

// Allocation test models. Handlers are deliberately trivial so the measurement reflects dispatch
// cost rather than handler work.
public record AllocGenPing(string Name) : IRequest<string>;
public record AllocGenSyncPing(string Name) : IRequest<string>;
public record AllocGenEvent(int Id) : INotification;

public class AllocGenPingHandler : IAsyncRequestHandler<AllocGenPing, string>
{
    public Task<string> HandleAsync(AllocGenPing request, CancellationToken cancellationToken)
        => Task.FromResult(request.Name);
}

public class AllocGenSyncPingHandler : IRequestHandler<AllocGenSyncPing, string>
{
    public string Handle(AllocGenSyncPing request) => request.Name;
}

public class AllocGenEventHandlerOne : INotificationHandler<AllocGenEvent>
{
    public Task Handle(AllocGenEvent notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class AllocGenEventHandlerTwo : INotificationHandler<AllocGenEvent>
{
    public Task Handle(AllocGenEvent notification, CancellationToken cancellationToken = default)
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
public static class GeneratedAllocationReport
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
