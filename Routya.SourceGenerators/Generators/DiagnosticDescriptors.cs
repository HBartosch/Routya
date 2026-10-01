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

        public static readonly DiagnosticDescriptor TypedDispatchNotGenerated = new DiagnosticDescriptor(
            id: "ROUTYA005",
            title: "No typed dispatch member generated for handler",
            messageFormat: "Handler '{0}' is registered, but no typed member was generated on IGeneratedRoutya because '{1}' is not publicly accessible. Dispatch it through IRoutya, or make the type public.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "IGeneratedRoutya and GeneratedRoutya are public, so they cannot expose a request, response or notification type that is not externally visible. The handler is still registered by AddGeneratedRoutya and remains reachable through runtime dispatch.");

        public static readonly DiagnosticDescriptor ReferencedHandlerNotAccessible = new DiagnosticDescriptor(
            id: "ROUTYA006",
            title: "Handler in a referenced assembly is not accessible",
            messageFormat: "Handler '{0}' in referenced assembly '{1}' is not public, so it cannot be registered from this assembly and has been skipped. Make it public, or register it from its own assembly.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Generated registration code lives in the consuming assembly, and a type that is not public in another assembly is not visible to it. Such a handler is skipped rather than silently missing at runtime.");

        public static readonly DiagnosticDescriptor DispatcherAlreadyProvided = new DiagnosticDescriptor(
            id: "ROUTYA007",
            title: "Generated dispatcher already provided by a referenced assembly",
            messageFormat: "Referenced assembly '{0}' already provides a generated Routya dispatcher, and this project declares no handlers of its own, so nothing was generated here. Use the dispatcher from '{0}'.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Generating a second dispatcher would put a duplicate Routya.Generated.IGeneratedRoutya in scope and produce CS0436 conflicts. This commonly happens in a test project that references the application project.");

        public static readonly DiagnosticDescriptor DispatcherNotRequested = new DiagnosticDescriptor(
            id: "ROUTYA008",
            title: "No generated dispatcher was requested",
            messageFormat: "Found {0} Routya handler(s), but this project never calls AddGeneratedRoutya, so no dispatcher was generated. Call AddGeneratedRoutya where the container is built, or set <RoutyaGenerateDispatcher>true</RoutyaGenerateDispatcher> to generate anyway.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "Routya.Generated is a fixed, public namespace, so only the project that composes the container should emit it. A library project that declares handlers and never builds a container is the normal layered shape and needs no dispatcher of its own.");

        public static readonly DiagnosticDescriptor DuplicateDispatcher = new DiagnosticDescriptor(
            id: "ROUTYA009",
            title: "More than one assembly generates a Routya dispatcher",
            messageFormat: "Referenced assembly '{0}' already generates a Routya dispatcher, and '{1}' generates one too, which will produce CS0436 type conflicts. Set <RoutyaGenerateDispatcher>false</RoutyaGenerateDispatcher> in whichever of the two does not compose the container.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Two assemblies in one reference chain cannot both define Routya.Generated.IGeneratedRoutya. The compiler resolves the conflict in favour of the source declaration, so a caller can silently bind to a dispatcher other than the one registered at startup.");

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
