namespace Routya.SourceGenerators.Test;

/// <summary>
/// Confirms the harness itself is wired correctly, so that a failure in any other test in this
/// project points at the generator rather than at the test infrastructure.
/// </summary>
public class GeneratorHarnessSanityTests
{
    [Fact]
    public void Async_Handler_In_A_Namespace_Generates_Compiling_Code()
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

        Assert.Contains("RoutyaGenerated.Registration.g.cs", result.GeneratedSources.Keys);
        Assert.Contains("RoutyaGenerated.Dispatcher.g.cs", result.GeneratedSources.Keys);
        Assert.Contains("SendAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Notification_Handlers_Generate_Compiling_Code()
    {
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class UserCreated : INotification { }

    public class SendEmail : INotificationHandler<UserCreated>
    {
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    public class WriteAudit : INotificationHandler<UserCreated>
    {
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}");

        result.AssertCompiles();

        Assert.Contains("PublishAsync", result.AllGeneratedSource);
    }

    [Fact]
    public void Input_That_Does_Not_Compile_Is_Reported_As_A_Test_Error()
    {
        // Guards the harness: a broken test input must be distinguishable from a generator defect.
        var result = GeneratorHarness.Run("this is not valid C#");

        Assert.False(result.InputErrors.IsEmpty);
    }
}
