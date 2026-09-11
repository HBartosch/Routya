using Microsoft.CodeAnalysis;

namespace Routya.SourceGenerators
{
    /// <summary>
    /// Single source of truth for rendering a type into generated code.
    /// </summary>
    /// <remarks>
    /// Generated code must name every type unambiguously, because it is compiled in the consumer's
    /// assembly alongside arbitrary user types. Hand building names from
    /// <c>ContainingNamespace</c> and <c>Name</c> loses containing types, so a nested type such as
    /// <c>Features.CreateOrder.Command</c> is emitted as <c>Features.Command</c>, and it cannot
    /// render arrays or constructed generics at all. Roslyn's own fully qualified format handles
    /// nested types, arrays, constructed generics, tuples and keyword aliases, and prefixes
    /// <c>global::</c> so a user type cannot shadow a framework one.
    /// </remarks>
    internal static class SymbolNames
    {
        private static readonly SymbolDisplayFormat FullyQualifiedFormat =
            SymbolDisplayFormat.FullyQualifiedFormat
                .WithMiscellaneousOptions(
                    SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
                    | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
                    | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        /// <summary>
        /// Renders a type as it should appear in generated source, for example
        /// <c>global::Sample.Features.CreateOrder.Command</c> or
        /// <c>global::System.Collections.Generic.List&lt;byte[]&gt;</c>.
        /// </summary>
        public static string ToGeneratedName(this ITypeSymbol symbol)
            => symbol.ToDisplayString(FullyQualifiedFormat);

        /// <summary>
        /// Determines whether <paramref name="candidate"/> is the given Routya handler interface,
        /// matched on namespace, name and arity rather than by string prefix.
        /// </summary>
        public static bool IsRoutyaInterface(this INamedTypeSymbol candidate, string name, int arity)
            => candidate.Arity == arity
               && candidate.Name == name
               && candidate.ContainingNamespace?.ToDisplayString() == RoutyaAbstractionsNamespace;

        public const string RoutyaAbstractionsNamespace = "Routya.Core.Abstractions";
    }
}
