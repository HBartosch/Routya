using Microsoft.CodeAnalysis;

namespace Routya.SourceGenerators.Generators
{
    internal static class DiagnosticDescriptors
    {
        private const string Category = "Routya.SourceGenerator";

        public static readonly DiagnosticDescriptor HandlerNotFound = new DiagnosticDescriptor(
            id: "ROUTYA001",
            title: "No handler found for request type",
            messageFormat: "No handler found for request type '{0}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Every request type must have exactly one handler registered.");

        public static readonly DiagnosticDescriptor MultipleHandlers = new DiagnosticDescriptor(
            id: "ROUTYA002",
            title: "Multiple handlers found for request type",
            messageFormat: "Multiple handlers found for request type '{0}': {1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Each request type should have only one handler.");

        public static readonly DiagnosticDescriptor HandlerDiscovered = new DiagnosticDescriptor(
            id: "ROUTYA003",
            title: "Handler discovered",
            messageFormat: "Discovered {0} handler: {1} -> {2}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Informational message about discovered handlers.");

        public static readonly DiagnosticDescriptor GenerationComplete = new DiagnosticDescriptor(
            id: "ROUTYA004",
            title: "Code generation complete",
            messageFormat: "Generated code for {0} handlers and {1} notification handlers",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Informational message about code generation completion.");
    }
}
