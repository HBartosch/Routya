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
