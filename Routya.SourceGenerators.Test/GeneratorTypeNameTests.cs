namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers how the generator renders request, response and handler type names, and how it behaves
/// when a type argument is not a named type.
/// </summary>
public class GeneratorTypeNameTests
{
    [Fact]
    public void Array_Response_Type_Does_Not_Crash_The_Generator()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class GetNames : IRequest<string[]> { }

    public class GetNamesHandler : IAsyncRequestHandler<GetNames, string[]>
    {
        public Task<string[]> HandleAsync(GetNames request, CancellationToken cancellationToken)
            => Task.FromResult(new string[0]);
    }
}");

        result.AssertCompiles();
        Assert.Contains("SendAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Array_Request_And_Nested_Array_Response_Do_Not_Crash_The_Generator()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class GetMatrix : IRequest<List<byte[]>> { }

    public class GetMatrixHandler : IAsyncRequestHandler<GetMatrix, List<byte[]>>
    {
        public Task<List<byte[]>> HandleAsync(GetMatrix request, CancellationToken cancellationToken)
            => Task.FromResult(new List<byte[]>());
    }
}");

        result.AssertCompiles();
    }

    [Fact]
    public void Nested_Request_And_Handler_Types_Generate_Compiling_Code()
    {
        // The feature folder convention: one containing type per feature, holding the request
        // and its handler.
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Features
{
    public static class CreateOrder
    {
        public class Command : IRequest<int> { }

        public class Handler : IAsyncRequestHandler<Command, int>
        {
            public Task<int> HandleAsync(Command request, CancellationToken cancellationToken)
                => Task.FromResult(1);
        }
    }
}");

        result.AssertCompiles();
        Assert.Contains("Sample.Features.CreateOrder.Command", result.AllGeneratedSource);
    }

    [Fact]
    public void Nested_Notification_Types_Generate_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample.Features
{
    public static class OrderShipped
    {
        public class Event : INotification { }

        public class NotifyCustomer : INotificationHandler<Event>
        {
            public Task Handle(Event notification, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}");

        result.AssertCompiles();
        Assert.Contains("Sample.Features.OrderShipped.Event", result.AllGeneratedSource);
    }

    [Fact]
    public void Generic_Response_Type_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class Item { }

    public class GetItems : IRequest<IReadOnlyList<Item>> { }

    public class GetItemsHandler : IAsyncRequestHandler<GetItems, IReadOnlyList<Item>>
    {
        public Task<IReadOnlyList<Item>> HandleAsync(GetItems request, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<Item>>(new List<Item>());
    }
}");

        result.AssertCompiles();
    }

    [Fact]
    public void Request_Type_In_The_Global_Namespace_Generates_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

public class Ping : IRequest<string> { }

public class PingHandler : IAsyncRequestHandler<Ping, string>
{
    public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
        => Task.FromResult(""pong"");
}");

        result.AssertCompiles();
    }

    [Fact]
    public void A_Type_Named_Like_A_Framework_Type_Does_Not_Collide()
    {
        // Without a global:: prefix, a user type called Task or String in scope breaks the
        // generated code.
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class String { }

    public class GetThing : IRequest<System.String> { }

    public class GetThingHandler : IAsyncRequestHandler<GetThing, System.String>
    {
        public Task<System.String> HandleAsync(GetThing request, CancellationToken cancellationToken)
            => Task.FromResult(""x"");
    }
}");

        result.AssertCompiles();
    }
}
