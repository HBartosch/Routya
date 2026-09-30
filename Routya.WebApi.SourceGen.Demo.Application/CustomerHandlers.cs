using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;

namespace Routya.WebApi.SourceGen.Demo.Application;

// These handlers live in a different assembly from the one the generator runs in. Before Routya
// found handlers in referenced assemblies, none of them would have been registered at all.

public record GetCustomerQuery(int Id) : IRequest<Customer>;

public record Customer(int Id, string Name, string InstanceId);

/// <summary>
/// Carries no lifetime attribute, so it follows whatever AddGeneratedRoutya was given. The Web
/// project passes Scoped, so this is one instance per HTTP request.
/// </summary>
public class GetCustomerHandler : IAsyncRequestHandler<GetCustomerQuery, Customer>
{
    // Included in the response so you can watch the lifetime behave
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public Task<Customer> HandleAsync(GetCustomerQuery request, CancellationToken ct)
        => Task.FromResult(new Customer(request.Id, $"Customer_{request.Id}", _instanceId));
}

public record GetServiceStatusQuery : IRequest<string>;

/// <summary>
/// Pinned to Singleton, overriding the Scoped passed to AddGeneratedRoutya. Safe here because it
/// holds only immutable state and depends on nothing scoped. A Singleton handler must not inject a
/// DbContext or anything else registered Scoped.
/// </summary>
[RoutyaHandler(ServiceLifetime.Singleton)]
public class GetServiceStatusHandler : IAsyncRequestHandler<GetServiceStatusQuery, string>
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;

    public Task<string> HandleAsync(GetServiceStatusQuery request, CancellationToken ct)
        => Task.FromResult($"instance {_instanceId}, up since {_startedAt:HH:mm:ss}");
}

public record CustomerRegistered(int Id) : INotification;

/// <summary>A notification handler in the referenced assembly, discovered the same way.</summary>
public class SendWelcomeEmailHandler : INotificationHandler<CustomerRegistered>
{
    public Task Handle(CustomerRegistered notification, CancellationToken ct = default)
    {
        Console.WriteLine($"  [Application assembly] welcome email queued for customer {notification.Id}");
        return Task.CompletedTask;
    }
}
