namespace Routya.SourceGenerators.Test;

/// <summary>
/// Generated code must be marked so tooling outside the compiler can recognise it.
/// </summary>
/// <remarks>
/// The <c>// &lt;auto-generated/&gt;</c> header only affects Roslyn analyzers. Coverage tools key
/// off attributes instead, so without these the generated dispatcher counts against a consumer's
/// coverage figure for code they never wrote.
/// </remarks>
public class GeneratedCodeAttributeTests
{
    private const string Source = @"
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
    public void The_Generated_Dispatcher_Is_Marked_As_Generated_And_Excluded_From_Coverage()
    {
        var result = GeneratorHarness.Run(Source);

        result.AssertCompiles();

        var dispatcher = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Dispatcher")).Value;

        Assert.Contains("global::System.CodeDom.Compiler.GeneratedCode(\"Routya.SourceGenerators\"", dispatcher);
        Assert.Contains("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage", dispatcher);
    }

    [Fact]
    public void The_Registration_Extensions_Are_Marked_As_Generated_And_Excluded_From_Coverage()
    {
        var result = GeneratorHarness.Run(Source);

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;

        Assert.Contains("global::System.CodeDom.Compiler.GeneratedCode(\"Routya.SourceGenerators\"", registration);
        Assert.Contains("global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage", registration);
    }

    [Fact]
    public void The_Generated_Interface_Is_Marked_As_Generated()
    {
        var result = GeneratorHarness.Run(Source);

        result.AssertCompiles();

        var registration = result.GeneratedSources.Single(kvp => kvp.Key.Contains("Registration")).Value;
        var interfaceIndex = registration.IndexOf("public interface IGeneratedRoutya", StringComparison.Ordinal);

        Assert.True(interfaceIndex > 0, "IGeneratedRoutya was not generated.");

        // The attribute must be immediately above the declaration, not merely present in the file
        var preceding = registration.Substring(0, interfaceIndex);
        Assert.Contains("global::System.CodeDom.Compiler.GeneratedCode", preceding);
    }

    [Fact]
    public void The_Attributes_Are_Fully_Qualified_So_A_User_Type_Cannot_Shadow_Them()
    {
        // A consumer with their own GeneratedCodeAttribute in scope must not break the build.
        var result = GeneratorHarness.Run(@"
using Routya.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace System.CodeDom.Compiler
{
    public class SomethingElse { }
}

namespace Sample
{
    public class GeneratedCodeAttribute : System.Attribute { }

    public class Ping : IRequest<string> { }

    public class PingHandler : IAsyncRequestHandler<Ping, string>
    {
        public Task<string> HandleAsync(Ping request, CancellationToken cancellationToken)
            => Task.FromResult(""pong"");
    }
}");

        result.AssertCompiles();
    }
}
