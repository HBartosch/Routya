using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Routya.SourceGenerators.Generators;

namespace Routya.SourceGenerators.Test;

/// <summary>
/// Runs <see cref="HandlerRegistrationGenerator"/> against source text in memory and reports both
/// what the generator produced and whether the resulting compilation is valid.
/// </summary>
/// <remarks>
/// This exists so that generator defects which break the consuming build can be expressed as
/// failing tests. The sibling Routya.SourceGen.Test project runs the generator over its own
/// compilation, so a generator that emits invalid code there fails the build rather than a test.
/// </remarks>
public static class GeneratorHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    /// <summary>
    /// Compiles <paramref name="referencedSource"/> into a separate assembly, then runs the
    /// generator over <paramref name="source"/> with that assembly referenced.
    /// </summary>
    /// <remarks>
    /// This is how a multiple project solution actually looks to the generator: the handler lives
    /// in another assembly and has no syntax tree in the compilation being generated for. A single
    /// source string cannot reproduce that, because everything shares one compilation.
    /// </remarks>
    public static GeneratorRunResult RunWithReferencedAssembly(string referencedSource, string source)
    {
        var referencedCompilation = CSharpCompilation.Create(
            assemblyName: "Routya.GeneratorHarness.Referenced",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(referencedSource, new CSharpParseOptions(LanguageVersion.Latest)) },
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var referencedErrors = referencedCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        if (!referencedErrors.IsEmpty)
        {
            throw new InvalidOperationException(
                "The referenced source does not compile on its own: "
                + string.Join(" | ", referencedErrors.Select(d => $"{d.Id}: {d.GetMessage()}")));
        }

        using var peStream = new MemoryStream();
        var emitResult = referencedCompilation.Emit(peStream);

        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                "The referenced source failed to emit: "
                + string.Join(" | ", emitResult.Diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));
        }

        peStream.Position = 0;

        return Run(source, References.Add(MetadataReference.CreateFromStream(peStream)));
    }

    public static GeneratorRunResult Run(string source) => Run(source, References);

    private static GeneratorRunResult Run(string source, ImmutableArray<MetadataReference> references)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create(
            assemblyName: "Routya.GeneratorHarness",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        // The input must be valid on its own, otherwise a later assertion about generated code
        // would be reporting a mistake in the test rather than a defect in the generator.
        var inputErrors = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        var driver = CSharpGeneratorDriver.Create(new HandlerRegistrationGenerator());

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        var generatedSources = outputCompilation
            .SyntaxTrees
            .Where(t => t != syntaxTree)
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.ToString());

        var outputErrors = outputCompilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new GeneratorRunResult(
            inputErrors,
            generatorDiagnostics,
            outputErrors,
            generatedSources);
    }

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        // Every assembly in this test project's closure, which includes Routya.Core and
        // Microsoft.Extensions.DependencyInjection.Abstractions.
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");

        if (string.IsNullOrEmpty(trusted))
        {
            throw new InvalidOperationException(
                "TRUSTED_PLATFORM_ASSEMBLIES is unavailable, so no metadata references can be built.");
        }

        return trusted!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}

/// <summary>The outcome of driving the generator over a single source file.</summary>
public sealed class GeneratorRunResult
{
    public GeneratorRunResult(
        ImmutableArray<Diagnostic> inputErrors,
        ImmutableArray<Diagnostic> generatorDiagnostics,
        ImmutableArray<Diagnostic> outputErrors,
        IReadOnlyDictionary<string, string> generatedSources)
    {
        InputErrors = inputErrors;
        GeneratorDiagnostics = generatorDiagnostics;
        OutputErrors = outputErrors;
        GeneratedSources = generatedSources;
    }

    /// <summary>Errors in the test's own input source, before the generator ran.</summary>
    public ImmutableArray<Diagnostic> InputErrors { get; }

    /// <summary>Diagnostics the generator reported, including CS8785 when the generator threw.</summary>
    public ImmutableArray<Diagnostic> GeneratorDiagnostics { get; }

    /// <summary>Errors in the compilation after the generated sources were added.</summary>
    public ImmutableArray<Diagnostic> OutputErrors { get; }

    /// <summary>Generated sources, keyed on hint name.</summary>
    public IReadOnlyDictionary<string, string> GeneratedSources { get; }

    public string AllGeneratedSource => string.Join("\n", GeneratedSources.Values);

    /// <summary>
    /// Asserts the input was valid, the generator did not crash, and the code it produced compiles.
    /// </summary>
    public void AssertCompiles()
    {
        Assert.True(
            InputErrors.IsEmpty,
            "The test input does not compile on its own:\n" + Format(InputErrors));

        var crashes = GeneratorDiagnostics
            .Where(d => d.Id == "CS8785" || d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        Assert.True(
            crashes.IsEmpty,
            "The generator reported errors:\n" + Format(crashes));

        Assert.True(
            OutputErrors.IsEmpty,
            "The generated code does not compile:\n" + Format(OutputErrors));
    }

    private static string Format(ImmutableArray<Diagnostic> diagnostics)
        => string.Join("\n", diagnostics.Select(d => $"  {d.Id}: {d.GetMessage()}"));
}
