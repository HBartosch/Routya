using Microsoft.CodeAnalysis;

namespace Routya.SourceGenerators.Models
{
    /// <summary>
    /// Metadata about a discovered pipeline behavior.
    /// </summary>
    internal sealed class BehaviorDescriptor
    {
        public INamedTypeSymbol BehaviorType { get; set; } = null!;
        public INamedTypeSymbol RequestType { get; set; } = null!;
        public INamedTypeSymbol ResponseType { get; set; } = null!;
    }
}
