using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.SourceGen.Test;

// ── Request / Response ──────────────────────────────────────────────────────

public record GetProductRequest(int Id) : IRequest<Product>;

public record Product(int Id, string Name);

public class GetProductHandler : IAsyncRequestHandler<GetProductRequest, Product>
{
    public Task<Product> HandleAsync(GetProductRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new Product(request.Id, $"Product_{request.Id}"));
}

// ── Synchronous request / response ──────────────────────────────────────────
// Implements IRequestHandler rather than IAsyncRequestHandler, so the generator emits a
// synchronous Send member for it rather than SendAsync.

public record CalculateTotalRequest(int Quantity, decimal UnitPrice) : IRequest<decimal>;

public class CalculateTotalHandler : IRequestHandler<CalculateTotalRequest, decimal>
{
    public decimal Handle(CalculateTotalRequest request) => request.Quantity * request.UnitPrice;
}

// ── Internal handler with a public request ──────────────────────────────────
// The Clean Architecture convention: the request is part of the public contract, the handler is
// an implementation detail. The generator must still discover and register it.

public record ArchiveOrderCommand(int OrderId) : IRequest<bool>;

internal sealed class ArchiveOrderHandler : IAsyncRequestHandler<ArchiveOrderCommand, bool>
{
    public Task<bool> HandleAsync(ArchiveOrderCommand request, CancellationToken cancellationToken)
        => Task.FromResult(request.OrderId > 0);
}

// ── Notification ────────────────────────────────────────────────────────────

public class ProductCreatedNotification : INotification
{
    public int ProductId { get; set; }
}

public class InventoryHandler : INotificationHandler<ProductCreatedNotification>
{
    private readonly HandlerCallTracker _tracker;

    public InventoryHandler(HandlerCallTracker tracker) => _tracker = tracker;

    public Task Handle(ProductCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        _tracker.Record(nameof(InventoryHandler));
        return Task.CompletedTask;
    }
}

public class AuditHandler : INotificationHandler<ProductCreatedNotification>
{
    private readonly HandlerCallTracker _tracker;

    public AuditHandler(HandlerCallTracker tracker) => _tracker = tracker;

    public Task Handle(ProductCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        _tracker.Record(nameof(AuditHandler));
        return Task.CompletedTask;
    }
}

// ── Notification fan out where one handler faults ───────────────────────────
// Exactly two handlers, which is the count the emitter used to special case by starting both
// tasks and then awaiting them one after another.

public class FanOutFailureNotification : INotification { }

public class FanOutThrowingHandler : INotificationHandler<FanOutFailureNotification>
{
    // Faults asynchronously rather than throwing synchronously, so that both handlers are
    // actually started and the test exercises how their tasks are awaited.
    public async Task Handle(FanOutFailureNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        throw new InvalidOperationException("handler failed");
    }
}

public class FanOutSlowHandler : INotificationHandler<FanOutFailureNotification>
{
    public static bool Completed { get; set; }

    public async Task Handle(FanOutFailureNotification notification, CancellationToken cancellationToken = default)
    {
        await Task.Delay(75, CancellationToken.None);
        Completed = true;
    }
}

// ── Test infrastructure ─────────────────────────────────────────────────────

/// <summary>Singleton injected into handlers so tests can observe invocations.</summary>
public class HandlerCallTracker
{
    private readonly System.Collections.Generic.List<string> _calls = new();

    public System.Collections.Generic.IReadOnlyList<string> Calls => _calls;

    public void Record(string handlerName) => _calls.Add(handlerName);

    public void Reset() => _calls.Clear();
}

/// <summary>Pipeline behavior that records its execution in the tracker.</summary>
public class TrackingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly HandlerCallTracker _tracker;

    public TrackingBehavior(HandlerCallTracker tracker) => _tracker = tracker;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _tracker.Record(nameof(TrackingBehavior<TRequest, TResponse>));
        return await next(cancellationToken);
    }
}
