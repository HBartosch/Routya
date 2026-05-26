using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.NuGet.IntegrationTest;

// ── Request / Response ──────────────────────────────────────────────────────

public record GetOrderRequest(int OrderId) : IRequest<Order>;

public record Order(int Id, string Status);

public class GetOrderHandler : IAsyncRequestHandler<GetOrderRequest, Order>
{
    public Task<Order> HandleAsync(GetOrderRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new Order(request.OrderId, $"Confirmed_{request.OrderId}"));
}

// ── Notification ─────────────────────────────────────────────────────────────

public class OrderShippedEvent : INotification
{
    public int OrderId { get; set; }
}

public class EmailNotificationHandler : INotificationHandler<OrderShippedEvent>
{
    private readonly InvocationTracker _tracker;
    public EmailNotificationHandler(InvocationTracker tracker) => _tracker = tracker;

    public Task Handle(OrderShippedEvent notification, CancellationToken cancellationToken = default)
    {
        _tracker.Record(nameof(EmailNotificationHandler));
        return Task.CompletedTask;
    }
}

public class WarehouseHandler : INotificationHandler<OrderShippedEvent>
{
    private readonly InvocationTracker _tracker;
    public WarehouseHandler(InvocationTracker tracker) => _tracker = tracker;

    public Task Handle(OrderShippedEvent notification, CancellationToken cancellationToken = default)
    {
        _tracker.Record(nameof(WarehouseHandler));
        return Task.CompletedTask;
    }
}

// ── Test infrastructure ──────────────────────────────────────────────────────

public class InvocationTracker
{
    private readonly List<string> _calls = new();
    public IReadOnlyList<string> Calls => _calls;
    public void Record(string name) => _calls.Add(name);
}

public class RecordingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly InvocationTracker _tracker;
    public RecordingBehavior(InvocationTracker tracker) => _tracker = tracker;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _tracker.Record(nameof(RecordingBehavior<TRequest, TResponse>));
        return await next(cancellationToken);
    }
}
