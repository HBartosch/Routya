namespace Routya.SourceGenerators.Test;

/// <summary>
/// Tests for which project owns the generated dispatcher.
/// </summary>
/// <remarks>
/// <c>Routya.Generated</c> is a fixed, public namespace, so two assemblies in one reference chain
/// cannot both define <c>IGeneratedRoutya</c>. The generator therefore emits into the project that
/// composes the container, identified by its call to <c>AddGeneratedRoutya</c>, and nowhere else.
/// </remarks>
public class GeneratorOptInTests
{
    private const string Handler = @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Sample
{
    public class Ping : IRequest<string> { }

    public class PingHandler : IAsyncRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(""pong"");
    }
}";

    [Fact]
    public void A_Project_That_Never_Calls_AddGeneratedRoutya_Gets_Nothing()
    {
        // The library project shape: it declares handlers but composes no container. Emitting a
        // dispatcher here is what caused a duplicate IGeneratedRoutya in every downstream project.
        var result = GeneratorHarness.Run(Handler, generateDispatcher: null);

        Assert.Empty(result.GeneratedSources);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ROUTYA008");
    }

    [Fact]
    public void Calling_AddGeneratedRoutya_Is_What_Triggers_Generation()
    {
        // The call resolves to a method that does not exist until the generator emits it, so the
        // decision has to be taken from syntax alone. This test pins that: the same source that
        // produced nothing above produces a dispatcher once the call is present, and the result
        // compiles, which proves the call bound to the generated method.
        var result = GeneratorHarness.Run(Handler + @"
namespace Sample
{
    using Routya.Generated;

    public static class Startup
    {
        public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            services.AddGeneratedRoutya();
        }
    }
}", generateDispatcher: null);

        // AssertCompiles is deliberately not used: it requires the input to compile on its own,
        // and this input cannot, because AddGeneratedRoutya does not exist until the generator has
        // run. That the output compiles is the whole point.
        Assert.NotEmpty(result.GeneratedSources);
        Assert.Empty(result.OutputErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ROUTYA008");
    }

    [Fact]
    public void The_MsBuild_Property_Can_Force_Generation_On()
    {
        // For a project that registers through a helper in another assembly, or by reflection,
        // where there is no call for the generator to see.
        var result = GeneratorHarness.Run(Handler, generateDispatcher: true);

        result.AssertCompiles();
        Assert.NotEmpty(result.GeneratedSources);
    }

    [Fact]
    public void The_MsBuild_Property_Can_Force_Generation_Off()
    {
        // An explicit false beats call detection, which is how a consumer resolves ROUTYA009.
        var result = GeneratorHarness.Run(Handler + @"
namespace Sample
{
    using Routya.Generated;

    public static class Startup
    {
        public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
        {
            services.AddGeneratedRoutya();
        }
    }
}", generateDispatcher: false);

        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void Two_Assemblies_Generating_A_Dispatcher_Is_Reported()
    {
        // Both projects opted in, and this project has handlers of its own so it cannot simply
        // defer to the upstream one. The conflict is real and CS0436 follows, so it is a warning
        // rather than a silent choice made on the consumer's behalf.
        var upstream = @"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Generated
{
    // Stands in for the dispatcher an upstream project already generated
    public interface IGeneratedRoutya { }
}

namespace Upstream
{
    // Present so the emitted assembly genuinely references Routya.Core. The generator skips any
    // assembly that does not, before enumerating a single type.
    public class Poke : IRequest<string> { }

    public class PokeHandler : IAsyncRequestHandler<Poke, string>
    {
        public Task<string> HandleAsync(Poke request, CancellationToken cancellationToken)
            => Task.FromResult(""poke"");
    }
}";

        var result = GeneratorHarness.RunWithReferencedAssembly(upstream, Handler, generateDispatcher: true);

        Assert.NotEmpty(result.GeneratedSources);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ROUTYA009");
    }
}
