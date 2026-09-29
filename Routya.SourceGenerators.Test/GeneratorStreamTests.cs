namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers source generation for <c>IStreamRequestHandler</c>, which gets a <c>CreateStream</c>
/// member rather than <c>SendAsync</c> or <c>Send</c>.
/// </summary>
public class GeneratorStreamTests
{
    private const string StreamHandlerSource = @"
using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class ExportProducts : IStreamRequest<string> { }

    public class ExportProductsHandler : IStreamRequestHandler<ExportProducts, string>
    {
        public async IAsyncEnumerable<string> Handle(
            ExportProducts request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return ""a"";
        }
    }
}";

    [Fact]
    public void Stream_Handler_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(StreamHandlerSource);

        result.AssertCompiles();
    }

    [Fact]
    public void Stream_Handler_Generates_A_CreateStream_Member_Returning_IAsyncEnumerable()
    {
        var result = GeneratorHarness.Run(StreamHandlerSource);

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;
        var dispatcher = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Dispatcher")).Value;

        Assert.Contains("CreateStream(", registration);
        Assert.Contains("CreateStream(", dispatcher);
        Assert.Contains("IAsyncEnumerable<string>", dispatcher);

        // A stream handler exposes Handle, not HandleAsync, and must not get a Task returning member
        Assert.DoesNotContain("HandleAsync", dispatcher);
        Assert.DoesNotContain("Task<string> SendAsync", dispatcher);
    }

    [Fact]
    public void Stream_Handler_Is_Registered_Against_IStreamRequestHandler()
    {
        var result = GeneratorHarness.Run(StreamHandlerSource);

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("IStreamRequestHandler<", registration);
        Assert.Contains("ExportProductsHandler", registration);
    }

    [Fact]
    public void Stream_Handler_With_A_Stream_Behavior_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class ExportProducts : IStreamRequest<string> { }

    public class ExportProductsHandler : IStreamRequestHandler<ExportProducts, string>
    {
        public async IAsyncEnumerable<string> Handle(
            ExportProducts request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return ""a"";
        }
    }

    public class LoggingStreamBehavior<TRequest, TResponse> : IStreamPipelineBehavior<TRequest, TResponse>
    {
        public IAsyncEnumerable<TResponse> Handle(
            TRequest request,
            StreamHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
            => next(cancellationToken);
    }
}");

        result.AssertCompiles();

        var dispatcher = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Dispatcher")).Value;
        Assert.Contains("IStreamPipelineBehavior<", dispatcher);
    }

    [Fact]
    public void Stream_Async_And_Sync_Handlers_Coexist_In_One_Compilation()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class Stream1 : IStreamRequest<int> { }
    public class Stream1Handler : IStreamRequestHandler<Stream1, int>
    {
        public async IAsyncEnumerable<int> Handle(Stream1 request, [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return 1;
        }
    }

    public class Async1 : IRequest<string> { }
    public class Async1Handler : IAsyncRequestHandler<Async1, string>
    {
        public Task<string> HandleAsync(Async1 request, CancellationToken ct) => Task.FromResult(""x"");
    }

    public class Sync1 : IRequest<int> { }
    public class Sync1Handler : IRequestHandler<Sync1, int>
    {
        public int Handle(Sync1 request) => 1;
    }
}");

        result.AssertCompiles();

        var dispatcher = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Dispatcher")).Value;
        Assert.Contains("CreateStream(", dispatcher);
        Assert.Contains("SendAsync(", dispatcher);
        Assert.Contains("Send(", dispatcher);
    }

    [Fact]
    public void Stream_Handler_With_A_Nested_Request_Type_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Features
{
    public static class ExportOrders
    {
        public class Query : IStreamRequest<int> { }

        public class Handler : IStreamRequestHandler<Query, int>
        {
            public async IAsyncEnumerable<int> Handle(Query request, [EnumeratorCancellation] CancellationToken ct)
            {
                await Task.Yield();
                yield return 1;
            }
        }
    }
}");

        result.AssertCompiles();
        Assert.Contains("Sample.Features.ExportOrders.Query", result.AllGeneratedSource);
    }
}
