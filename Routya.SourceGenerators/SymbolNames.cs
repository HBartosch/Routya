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

        /// <summary>
        /// Determines whether a type may appear in the signature of a public member.
        /// </summary>
        /// <remarks>
        /// The generated interface and dispatcher are public, so a type that is not externally
        /// visible cannot appear in their signatures without CS0051 or CS0050. Accessibility of a
        /// nested type depends on every containing type, and of a constructed generic on every type
        /// argument, so both are walked here.
        /// </remarks>
        public static bool IsExternallyVisible(this ITypeSymbol type)
        {
            switch (type)
            {
                case IArrayTypeSymbol array:
                    return array.ElementType.IsExternallyVisible();

                case IPointerTypeSymbol pointer:
                    return pointer.PointedAtType.IsExternallyVisible();

                // A type parameter's visibility is governed by the member that declares it
                case ITypeParameterSymbol _:
                    return true;

                case INamedTypeSymbol named:
                    for (var containing = named; containing != null; containing = containing.ContainingType)
                    {
                        if (!IsPubliclyAccessible(containing.DeclaredAccessibility))
                        {
                            return false;
                        }
                    }

                    foreach (var argument in named.TypeArguments)
                    {
                        if (!argument.IsExternallyVisible())
                        {
                            return false;
                        }
                    }

                    return true;

                default:
                    return IsPubliclyAccessible(type.DeclaredAccessibility);
            }
        }

        private static bool IsPubliclyAccessible(Accessibility accessibility)
            // NotApplicable covers the special types, such as the ones behind the int and string
            // keywords, which carry no declared accessibility of their own.
            => accessibility == Accessibility.Public || accessibility == Accessibility.NotApplicable;
    }
}
