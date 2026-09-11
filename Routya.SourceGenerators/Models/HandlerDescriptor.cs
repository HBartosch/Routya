using Microsoft.CodeAnalysis;

namespace Routya.SourceGenerators.Models
{
    /// <summary>
    /// Service lifetime enum (mirrors Microsoft.Extensions.DependencyInjection.ServiceLifetime).
    /// </summary>
    internal enum ServiceLifetime
    {
        Singleton,
        Scoped,
        Transient
    }

    /// <summary>
    /// Metadata about a discovered handler.
    /// </summary>
    internal sealed class HandlerDescriptor
    {
        public INamedTypeSymbol HandlerType { get; set; } = null!;

        // Request and response are ITypeSymbol rather than INamedTypeSymbol because a type argument
        // is not necessarily a named type. IRequest<string[]> yields an IArrayTypeSymbol, and a
        // generic handler yields an ITypeParameterSymbol. Casting either to INamedTypeSymbol threw
        // an InvalidCastException, which Roslyn surfaced as CS8785 and which stopped the generator
        // from contributing any output at all.
        public ITypeSymbol RequestType { get; set; } = null!;
        public ITypeSymbol? ResponseType { get; set; }

        public bool IsAsync { get; set; }
        public bool IsNotification { get; set; }
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
        public string HandlerInterfaceName { get; set; } = null!;

        /// <summary>
        /// Gets the handler type name as it should appear in generated source, for example
        /// "global::MyNamespace.MyFeature.Handler".
        /// </summary>
        public string ConcreteType => HandlerType.ToGeneratedName();
    }
}
