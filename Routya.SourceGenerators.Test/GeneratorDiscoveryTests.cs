namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers which handler declarations the generator discovers. Handlers that are not discovered
/// are not registered and get no typed dispatch member, and previously that happened silently.
/// </summary>
public class GeneratorDiscoveryTests
{
    [Fact]
    public void Internal_Handler_With_A_Public_Request_Is_Discovered()
    {
        // The common Clean Architecture shape: the request is part of the public contract and the
        // handler is an implementation detail.
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class GetUser : IRequest<string> { }

    internal sealed class GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}");

        result.AssertCompiles();

        Assert.Contains("GetUserHandler", result.AllGeneratedSource);
        Assert.Contains("SendAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Internal_Notification_Handler_Is_Discovered()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class UserCreated : INotification { }

    internal sealed class SendEmail : INotificationHandler<UserCreated>
    {
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}");

        result.AssertCompiles();

        Assert.Contains("SendEmail", result.AllGeneratedSource);
        Assert.Contains("PublishAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Internal_Handler_Nested_In_A_Public_Type_Is_Discovered()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Features
{
    public static class CreateOrder
    {
        public class Command : IRequest<int> { }

        internal sealed class Handler : IAsyncRequestHandler<Command, int>
        {
            public Task<int> HandleAsync(Command request, CancellationToken cancellationToken)
                => Task.FromResult(1);
        }
    }
}");

        result.AssertCompiles();

        Assert.Contains("Sample.Features.CreateOrder.Handler", result.AllGeneratedSource);
    }

    [Fact]
    public void Record_Handler_Is_Discovered()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public record GetUser : IRequest<string>;

    public record GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}");

        result.AssertCompiles();

        Assert.Contains("GetUserHandler", result.AllGeneratedSource);
    }

    [Fact]
    public void Internal_Request_Type_Still_Compiles_And_Is_Reported()
    {
        // The generated interface and dispatcher are public, so they cannot expose an internal
        // request type in a signature. The handler is still registered, and the user is told why
        // no typed member appeared rather than being left to wonder.
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    internal sealed class HiddenRequest : IRequest<int> { }

    internal sealed class HiddenHandler : IAsyncRequestHandler<HiddenRequest, int>
    {
        public Task<int> HandleAsync(HiddenRequest request, CancellationToken cancellationToken)
            => Task.FromResult(1);
    }
}");

        result.AssertCompiles();

        // Registered, so runtime dispatch through IRoutya still works
        Assert.Contains("HiddenHandler", result.AllGeneratedSource);

        // But no typed member on the public surface
        var dispatcher = result.GeneratedSources
            .Single(kvp => kvp.Key.Contains("Dispatcher")).Value;
        Assert.DoesNotContain("HiddenRequest request", dispatcher);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ROUTYA005");
    }

    // There is deliberately no test for a public request with an internal response. C# forbids it:
    // a public type cannot implement IRequest<T> when T is internal, because the base interface
    // must be at least as accessible as the type implementing it. An inaccessible response
    // therefore implies an inaccessible request.

    [Fact]
    public void Internal_Notification_Type_Still_Compiles_And_Is_Reported()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    internal sealed class HiddenEvent : INotification { }

    internal sealed class HiddenEventHandler : INotificationHandler<HiddenEvent>
    {
        public Task Handle(HiddenEvent notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}");

        result.AssertCompiles();

        Assert.Contains("HiddenEventHandler", result.AllGeneratedSource);

        var dispatcher = result.GeneratedSources
            .Single(kvp => kvp.Key.Contains("Dispatcher")).Value;
        Assert.DoesNotContain("HiddenEvent notification", dispatcher);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ROUTYA005");
    }

    [Fact]
    public void A_Public_Handler_Reports_No_Visibility_Diagnostic()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class GetUser : IRequest<string> { }

    public class GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}");

        result.AssertCompiles();

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ROUTYA005");
    }
}
