namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers handlers that live in a referenced assembly rather than the compilation being generated
/// for. This is the normal shape of a Clean Architecture solution, where handlers sit in an
/// Application project and the generator runs in the Web or API project.
/// </summary>
public class GeneratorReferencedAssemblyTests
{
    private const string HandlerInOtherAssembly = @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Users
{
    public class GetUser : IRequest<string> { }

    public class GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}";

    [Fact]
    public void Handler_In_A_Referenced_Assembly_Is_Discovered()
    {
        // The consuming project contains no handler of its own, exactly as a thin API project does.
        var result = GeneratorHarness.RunWithReferencedAssembly(
            HandlerInOtherAssembly,
            @"
namespace Api
{
    public class Marker { }
}");

        result.AssertCompiles();

        Assert.Contains("GetUserHandler", result.AllGeneratedSource);
        Assert.Contains("SendAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Notification_Handler_In_A_Referenced_Assembly_Is_Discovered()
    {
        var result = GeneratorHarness.RunWithReferencedAssembly(
            @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Users
{
    public class UserCreated : INotification { }

    public class SendEmail : INotificationHandler<UserCreated>
    {
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}",
            @"
namespace Api
{
    public class Marker { }
}");

        result.AssertCompiles();

        Assert.Contains("SendEmail", result.AllGeneratedSource);
        Assert.Contains("PublishAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Handlers_From_Both_Assemblies_Are_Discovered_Together()
    {
        // A solution usually has most handlers in another project and a few local ones.
        var result = GeneratorHarness.RunWithReferencedAssembly(
            HandlerInOtherAssembly,
            @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Api
{
    public class Ping : IRequest<string> { }

    public class PingHandler : IAsyncRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(""pong"");
    }
}");

        result.AssertCompiles();

        Assert.Contains("GetUserHandler", result.AllGeneratedSource);
        Assert.Contains("PingHandler", result.AllGeneratedSource);
    }

    [Fact]
    public void Internal_Handler_In_A_Referenced_Assembly_Is_Skipped_And_Reported()
    {
        // Generated registration lives in the consuming assembly, so an internal type in another
        // assembly is genuinely unreachable. The point is that the user is told, rather than
        // discovering a missing service at runtime.
        var result = GeneratorHarness.RunWithReferencedAssembly(
            @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Users
{
    public class GetUser : IRequest<string> { }

    internal sealed class GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}",
            @"
namespace Api
{
    public class Marker { }
}");

        result.AssertCompiles();

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ROUTYA006");
        Assert.DoesNotContain("GetUserHandler", result.AllGeneratedSource);
    }

    [Fact]
    public void A_Public_Handler_In_A_Referenced_Assembly_Reports_No_Accessibility_Diagnostic()
    {
        var result = GeneratorHarness.RunWithReferencedAssembly(
            HandlerInOtherAssembly,
            @"
namespace Api
{
    public class Marker { }
}");

        result.AssertCompiles();

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ROUTYA006");
    }

    [Fact]
    public void An_Assembly_With_No_Routya_Handlers_Contributes_Nothing()
    {
        // Guards against scanning every referenced assembly and picking up unrelated types.
        var result = GeneratorHarness.RunWithReferencedAssembly(
            @"
namespace ThirdParty
{
    public class SomeUnrelatedType { }

    public class AnotherType { }
}",
            @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Api
{
    public class Ping : IRequest<string> { }

    public class PingHandler : IAsyncRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(""pong"");
    }
}");

        result.AssertCompiles();

        Assert.Contains("PingHandler", result.AllGeneratedSource);
        Assert.DoesNotContain("SomeUnrelatedType", result.AllGeneratedSource);
        Assert.DoesNotContain("AnotherType", result.AllGeneratedSource);
    }
}
