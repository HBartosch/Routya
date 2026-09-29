using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Generated;

namespace Routya.Aot.Benchmark;

internal class Program
{
    public static void Main(string[] args) => BenchmarkRunner.Run<AotDispatchBenchmarks>(args: args);
}

/// <summary>
/// Runs the source generated dispatch path under both the ordinary JIT runtime and Native AOT, so
/// the two can be compared directly.
/// </summary>
/// <remarks>
/// Only the source generated path is measured. The runtime dispatcher relies on assembly scanning
/// and reflection based registration, which is not Native AOT compatible, and MediatR cannot be
/// included because it resolves handlers reflectively.
///
/// Pipeline behaviors are registered closed rather than as open generics. Microsoft's DI container
/// cannot construct an open generic service under Native AOT when a type argument is a value type,
/// and a closed registration keeps this benchmark valid for both value and reference type responses.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, baseline: true)]
[SimpleJob(RuntimeMoniker.NativeAot80)]
public class AotDispatchBenchmarks
{
    private IServiceProvider _provider = null!;
    private IGeneratedRoutya _routya = null!;
    private readonly GetGreeting _request = new("Benchmark");
    private readonly CalculateTotal _valueRequest = new(3, 9.99m);
    private readonly OrderPlaced _notification = new(42);

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddTransient<IPipelineBehavior<GetGreeting, string>, LoggingBehavior<GetGreeting, string>>();
        services.AddTransient<IPipelineBehavior<CalculateTotal, decimal>, LoggingBehavior<CalculateTotal, decimal>>();
        services.AddGeneratedRoutya();

        _provider = services.BuildServiceProvider();
        _routya = _provider.GetRequiredService<IGeneratedRoutya>();
    }

    [Benchmark(Description = "SendAsync, reference type response")]
    public Task<string> SendAsync_Reference() => _routya.SendAsync(_request);

    [Benchmark(Description = "Send, value type response")]
    public decimal Send_Value() => _routya.Send(_valueRequest);

    [Benchmark(Description = "PublishAsync, two handlers")]
    public Task PublishAsync_TwoHandlers() => _routya.PublishAsync(_notification);
}

// ── Messages and handlers ───────────────────────────────────────────────────

public record GetGreeting(string Name) : IRequest<string>;

public class GetGreetingHandler : IAsyncRequestHandler<GetGreeting, string>
{
    public Task<string> HandleAsync(GetGreeting request, CancellationToken cancellationToken)
        => Task.FromResult($"Hello, {request.Name}");
}

public record CalculateTotal(int Quantity, decimal UnitPrice) : IRequest<decimal>;

public class CalculateTotalHandler : IRequestHandler<CalculateTotal, decimal>
{
    public decimal Handle(CalculateTotal request) => request.Quantity * request.UnitPrice;
}

public record OrderPlaced(int Id) : INotification;

public class AuditHandler : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class EmailHandler : INotificationHandler<OrderPlaced>
{
    public Task Handle(OrderPlaced notification, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        => next(cancellationToken);
}
