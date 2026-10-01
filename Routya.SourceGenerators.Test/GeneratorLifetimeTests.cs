namespace Routya.SourceGenerators.Test;

/// <summary>
/// Covers the handler lifetime the generator registers, which is configurable through the
/// <c>AddGeneratedRoutya</c> parameter and overridable per handler with <c>RoutyaHandler</c>.
/// </summary>
/// <remarks>
/// The generated default is Transient while the runtime <c>AddRoutya</c> defaults to Scoped, so a
/// project moving from runtime dispatch to the generator changes handler lifetime unless it says
/// otherwise. The defaults are pinned here so that difference cannot drift further unnoticed.
/// </remarks>
public class GeneratorLifetimeTests
{
    private const string PlainHandler = @"
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
}";

    [Fact]
    public void AddGeneratedRoutya_Takes_A_Lifetime_Parameter_Defaulting_To_Transient()
    {
        var result = GeneratorHarness.Run(PlainHandler);

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("ServiceLifetime handlerLifetime = ServiceLifetime.Transient", registration);
    }

    [Fact]
    public void A_Handler_Without_An_Attribute_Defers_To_The_Parameter()
    {
        var result = GeneratorHarness.Run(PlainHandler);

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        // Registered through a ServiceDescriptor carrying the parameter, not a fixed AddTransient
        Assert.Contains("handlerLifetime", registration);
        Assert.DoesNotContain("services.AddTransient<global::Sample.GetUserHandler>()", registration);
    }

    [Fact]
    public void A_RoutyaHandler_Attribute_Fixes_That_Handler_Regardless_Of_The_Parameter()
    {
        var result = GeneratorHarness.Run(@"
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class GetUser : IRequest<string> { }

    [RoutyaHandler(ServiceLifetime.Singleton)]
    public class GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}");

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("services.AddSingleton<global::Sample.GetUserHandler>()", registration);
    }

    [Fact]
    public void An_Attributed_And_An_Unattributed_Handler_Can_Coexist()
    {
        var result = GeneratorHarness.Run(@"
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class Plain : IRequest<string> { }

    public class PlainHandler : IAsyncRequestHandler<Plain, string>
    {
        public Task<string> HandleAsync(Plain request, CancellationToken cancellationToken)
            => Task.FromResult(""a"");
    }

    public class Fixed : IRequest<string> { }

    [RoutyaHandler(ServiceLifetime.Scoped)]
    public class FixedHandler : IAsyncRequestHandler<Fixed, string>
    {
        public Task<string> HandleAsync(Fixed request, CancellationToken cancellationToken)
            => Task.FromResult(""b"");
    }
}");

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("services.AddScoped<global::Sample.FixedHandler>()", registration);
        Assert.Contains("typeof(global::Sample.PlainHandler), handlerLifetime", registration);
    }

    [Fact]
    public void A_Notification_Handler_Honours_The_Attribute_Too()
    {
        var result = GeneratorHarness.Run(@"
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class UserCreated : INotification { }

    [RoutyaHandler(ServiceLifetime.Singleton)]
    public class SendEmail : INotificationHandler<UserCreated>
    {
        public Task Handle(UserCreated notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}");

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("services.AddSingleton<global::Sample.SendEmail>()", registration);
    }

    [Fact]
    public void An_Attributed_Handler_In_A_Referenced_Assembly_Is_Honoured()
    {
        var result = GeneratorHarness.RunWithReferencedAssembly(@"
using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Users
{
    public class GetUser : IRequest<string> { }

    [RoutyaHandler(ServiceLifetime.Scoped)]
    public class GetUserHandler : IAsyncRequestHandler<GetUser, string>
    {
        public Task<string> HandleAsync(GetUser request, CancellationToken cancellationToken)
            => Task.FromResult(""user"");
    }
}", @"
namespace Api
{
    public class Marker { }
}");

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("services.AddScoped<global::Application.Users.GetUserHandler>()", registration);
    }
}
