namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers synchronous handlers, which implement IRequestHandler rather than
/// IAsyncRequestHandler. The generated interface declares a synchronous Send member for these,
/// so the generated dispatcher has to provide one.
/// </summary>
public class GeneratorSyncHandlerTests
{
    private const string SyncHandlerSource = @"
using Routya.Core.Abstractions;

namespace Sample
{
    public class Ping : IRequest<string> { }

    public class PingHandler : IRequestHandler<Ping, string>
    {
        public string Handle(Ping request) => ""pong"";
    }
}";

    [Fact]
    public void Sync_Handler_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(SyncHandlerSource);

        result.AssertCompiles();
    }

    [Fact]
    public void Sync_Handler_Generates_A_Send_Member_On_The_Interface_And_The_Dispatcher()
    {
        var result = GeneratorHarness.Run(SyncHandlerSource);

        result.AssertCompiles();

        var registration = result.GeneratedSources
            .Single(kvp => kvp.Key.Contains("Registration")).Value;
        var dispatcher = result.GeneratedSources
            .Single(kvp => kvp.Key.Contains("Dispatcher")).Value;

        Assert.Contains("Send(", registration);
        Assert.Contains("Send(", dispatcher);

        // A synchronous handler has no HandleAsync method, so the dispatcher must not call one
        Assert.DoesNotContain("HandleAsync", dispatcher);
    }

    [Fact]
    public void Sync_Handler_With_A_Pipeline_Behavior_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class Ping : IRequest<string> { }

    public class PingHandler : IRequestHandler<Ping, string>
    {
        public string Handle(Ping request) => ""pong"";
    }

    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
            => next(ct);
    }
}");

        result.AssertCompiles();
    }

    [Fact]
    public void Sync_And_Async_Handlers_In_One_Compilation_Generate_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class SyncPing : IRequest<string> { }

    public class SyncPingHandler : IRequestHandler<SyncPing, string>
    {
        public string Handle(SyncPing request) => ""sync"";
    }

    public class AsyncPing : IRequest<string> { }

    public class AsyncPingHandler : IAsyncRequestHandler<AsyncPing, string>
    {
        public Task<string> HandleAsync(AsyncPing request, CancellationToken cancellationToken)
            => Task.FromResult(""async"");
    }
}");

        result.AssertCompiles();

        var dispatcher = result.GeneratedSources
            .Single(kvp => kvp.Key.Contains("Dispatcher")).Value;

        Assert.Contains("Send(", dispatcher);
        Assert.Contains("SendAsync(", dispatcher);
    }

    [Fact]
    public void Sync_Handler_With_A_Nested_Request_Type_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;

namespace Sample.Features
{
    public static class Calculate
    {
        public class Query : IRequest<int> { }

        public class Handler : IRequestHandler<Query, int>
        {
            public int Handle(Query request) => 1;
        }
    }
}");

        result.AssertCompiles();
    }
}
