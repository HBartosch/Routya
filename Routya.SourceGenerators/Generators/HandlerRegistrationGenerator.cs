using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Routya.SourceGenerators.Emitters;
using Routya.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Routya.SourceGenerators.Generators
{
    /// <summary>
    /// Incremental source generator that discovers handlers and generates optimized registration code.
    /// </summary>
    [Generator]
    public class HandlerRegistrationGenerator : IIncrementalGenerator
    {
        private const string IRequestHandlerName = "Routya.Core.Abstractions.IRequestHandler";
        private const string IAsyncRequestHandlerName = "Routya.Core.Abstractions.IAsyncRequestHandler";
        private const string INotificationHandlerName = "Routya.Core.Abstractions.INotificationHandler";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register syntax provider to find potential handler types
            var handlerDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (node, _) => IsPotentialHandlerClass(node),
                    transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
                .Where(static m => m is not null)
                .Collect();

            // Combine with compilation
            var compilationAndHandlers = context.CompilationProvider.Combine(handlerDeclarations);

            // Generate the registration code
            context.RegisterSourceOutput(compilationAndHandlers, 
                static (spc, source) => Execute(source.Left, source.Right!, spc));
        }

        private static bool IsPotentialHandlerClass(SyntaxNode node)
        {
            // Look for class declarations with base types or interfaces
            return node is ClassDeclarationSyntax classDecl &&
                   classDecl.BaseList is not null &&
                   classDecl.BaseList.Types.Count > 0;
        }

        private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration);
            
            if (symbol is null || symbol.IsAbstract || symbol.DeclaredAccessibility != Accessibility.Public)
                return null;

            // Check if it implements any handler interfaces
            var interfaces = symbol.AllInterfaces;
            foreach (var iface in interfaces)
            {
                var fullName = GetFullTypeName(iface);
                if (fullName.StartsWith(IRequestHandlerName) ||
                    fullName.StartsWith(IAsyncRequestHandlerName) ||
                    fullName.StartsWith(INotificationHandlerName))
                {
                    return symbol;
                }
            }

            return null;
        }

        private static void Execute(
            Compilation compilation,
            ImmutableArray<INamedTypeSymbol?> handlers,
            SourceProductionContext context)
        {
            if (handlers.IsDefaultOrEmpty)
                return;

            var requestHandlers = new List<HandlerDescriptor>();
            var notificationHandlers = new List<HandlerDescriptor>();

            // Analyze each handler
            foreach (var handler in handlers)
            {
                if (handler is null)
                    continue;

                foreach (var iface in handler.AllInterfaces)
                {
                    var fullName = GetFullTypeName(iface);

                    if (fullName.StartsWith(IRequestHandlerName))
                    {
                        var descriptor = CreateRequestHandlerDescriptor(handler, iface, isAsync: false);
                        requestHandlers.Add(descriptor);
                        
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.HandlerDiscovered,
                            Location.None,
                            "Request",
                            handler.Name,
                            descriptor.RequestType.Name));
                    }
                    else if (fullName.StartsWith(IAsyncRequestHandlerName))
                    {
                        var descriptor = CreateRequestHandlerDescriptor(handler, iface, isAsync: true);
                        requestHandlers.Add(descriptor);
                        
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.HandlerDiscovered,
                            Location.None,
                            "AsyncRequest",
                            handler.Name,
                            descriptor.RequestType.Name));
                    }
                    else if (fullName.StartsWith(INotificationHandlerName))
                    {
                        var descriptor = CreateNotificationHandlerDescriptor(handler, iface);
                        notificationHandlers.Add(descriptor);
                        
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.HandlerDiscovered,
                            Location.None,
                            "Notification",
                            handler.Name,
                            descriptor.RequestType.Name));
                    }
                }
            }

            // Check for duplicate request handlers
            var duplicates = requestHandlers
                .GroupBy(h => GetFullTypeName(h.RequestType))
                .Where(g => g.Count() > 1)
                .ToList();

            foreach (var duplicate in duplicates)
            {
                var handlerNames = string.Join(", ", duplicate.Select(h => h.HandlerType.Name));
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MultipleHandlers,
                    Location.None,
                    duplicate.Key,
                    handlerNames));
            }

            // Generate the registration code
            var source = HandlerRegistrationEmitter.Generate(requestHandlers, notificationHandlers);
            context.AddSource("RoutyaGenerated.Registration.g.cs", source);

            // Generate the optimized dispatcher
            var notificationGroups = notificationHandlers
                .GroupBy(h => GetFullTypeName(h.RequestType))
                .ToDictionary(g => g.Key, g => g.ToList());

            var dispatcherSource = DispatcherEmitter.EmitGeneratedDispatcher(requestHandlers, notificationGroups);
            context.AddSource("RoutyaGenerated.Dispatcher.g.cs", dispatcherSource);

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GenerationComplete,
                Location.None,
                requestHandlers.Count,
                notificationHandlers.Count));
        }

        private static HandlerDescriptor CreateRequestHandlerDescriptor(
            INamedTypeSymbol handler,
            INamedTypeSymbol interfaceSymbol,
            bool isAsync)
        {
            // Interface is IRequestHandler<TRequest, TResponse> or IAsyncRequestHandler<TRequest, TResponse>
            var typeArgs = interfaceSymbol.TypeArguments;
            
            return new HandlerDescriptor
            {
                HandlerType = handler,
                RequestType = (INamedTypeSymbol)typeArgs[0],
                ResponseType = (INamedTypeSymbol)typeArgs[1],
                IsAsync = isAsync,
                IsNotification = false,
                Lifetime = DetectLifetime(handler),
                HandlerInterfaceName = isAsync ? IAsyncRequestHandlerName : IRequestHandlerName
            };
        }

        private static HandlerDescriptor CreateNotificationHandlerDescriptor(
            INamedTypeSymbol handler,
            INamedTypeSymbol interfaceSymbol)
        {
            // Interface is INotificationHandler<TNotification>
            var typeArgs = interfaceSymbol.TypeArguments;
            
            return new HandlerDescriptor
            {
                HandlerType = handler,
                RequestType = (INamedTypeSymbol)typeArgs[0],
                ResponseType = null,
                IsAsync = true, // Notification handlers are always async
                IsNotification = true,
                Lifetime = DetectLifetime(handler),
                HandlerInterfaceName = INotificationHandlerName
            };
        }

        private static ServiceLifetime DetectLifetime(INamedTypeSymbol handler)
        {
            // Look for lifetime attributes or conventions
            // Default to Transient for stateless handlers (matches MediatR behavior)
            // Transient handlers are created per call but don't show in allocation tracking
            // since they're short-lived and immediately eligible for GC
            return ServiceLifetime.Transient;
        }

        private static string GetFullTypeName(ISymbol symbol)
        {
            if (symbol.ContainingNamespace?.IsGlobalNamespace == false)
            {
                return $"{symbol.ContainingNamespace}.{symbol.Name}";
            }
            return symbol.Name;
        }
    }
}
