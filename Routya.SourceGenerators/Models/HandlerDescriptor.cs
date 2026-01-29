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
        public INamedTypeSymbol RequestType { get; set; } = null!;
        public INamedTypeSymbol? ResponseType { get; set; }
        public bool IsAsync { get; set; }
        public bool IsNotification { get; set; }
        public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Transient;
        public string HandlerInterfaceName { get; set; } = null!;

        /// <summary>
        /// Gets the fully qualified handler type name (e.g., "MyNamespace.MyHandler").
        /// </summary>
        public string ConcreteType => GetFullTypeName(HandlerType);

        private static string GetFullTypeName(INamedTypeSymbol symbol)
        {
            if (symbol.ContainingNamespace?.IsGlobalNamespace == false)
            {
                return $"{symbol.ContainingNamespace}.{symbol.Name}";
            }
            return symbol.Name;
        }
    }
}
