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
        private const string IStreamRequestHandlerName = "Routya.Core.Abstractions.IStreamRequestHandler";
        private const string RoutyaCoreAssemblyName = "Routya.Core";

        private static bool IsRequestHandler(INamedTypeSymbol iface)
            => iface.IsRoutyaInterface("IRequestHandler", arity: 2);

        private static bool IsAsyncRequestHandler(INamedTypeSymbol iface)
            => iface.IsRoutyaInterface("IAsyncRequestHandler", arity: 2);

        private static bool IsNotificationHandler(INamedTypeSymbol iface)
            => iface.IsRoutyaInterface("INotificationHandler", arity: 1);

        private static bool IsStreamRequestHandler(INamedTypeSymbol iface)
            => iface.IsRoutyaInterface("IStreamRequestHandler", arity: 2);

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
            // TypeDeclarationSyntax rather than ClassDeclarationSyntax, so that a handler declared
            // as a record or a struct is considered too. Interfaces are excluded below by IsAbstract.
            return node is TypeDeclarationSyntax typeDecl &&
                   typeDecl.BaseList is not null &&
                   typeDecl.BaseList.Types.Count > 0;
        }

        private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var typeDeclaration = (TypeDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

            // Accessibility is deliberately not filtered here. An internal handler is registered and
            // dispatched from inside generated method bodies in the same assembly, so it works, and
            // "internal sealed class Handler" is a common convention. Whether a typed member can be
            // generated for it is decided later, by HandlerDescriptor.SupportsTypedDispatch.
            if (symbol is null || symbol.IsAbstract)
                return null;

            // Check if it implements any handler interfaces
            var interfaces = symbol.AllInterfaces;
            foreach (var iface in interfaces)
            {
                if (IsRequestHandler(iface) || IsAsyncRequestHandler(iface)
                    || IsNotificationHandler(iface) || IsStreamRequestHandler(iface))
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
            // Deliberately no early return on an empty local handler set. A thin API project that
            // holds no handlers of its own but references an Application project that does is the
            // normal Clean Architecture layout, and returning here would generate nothing for it.
            var requestHandlers = new List<HandlerDescriptor>();
            var notificationHandlers = new List<HandlerDescriptor>();

            // Handlers declared in this compilation come from the syntax provider. Handlers in
            // referenced assemblies have no syntax tree here, so they have to be read from metadata.
            var allHandlers = handlers
                .Where(h => h is not null)
                .Concat(GetHandlersFromReferencedAssemblies(compilation, context))
                .ToList();

            // Analyze each handler
            foreach (var handler in allHandlers)
            {
                if (handler is null)
                    continue;

                foreach (var iface in handler.AllInterfaces)
                {
                    if (IsRequestHandler(iface))
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
                    else if (IsAsyncRequestHandler(iface))
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
                    else if (IsStreamRequestHandler(iface))
                    {
                        var descriptor = CreateRequestHandlerDescriptor(handler, iface, isAsync: true);
                        descriptor.IsStream = true;
                        descriptor.HandlerInterfaceName = IStreamRequestHandlerName;
                        requestHandlers.Add(descriptor);

                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.HandlerDiscovered,
                            Location.None,
                            "StreamRequest",
                            handler.Name,
                            descriptor.RequestType.Name));
                    }
                    else if (IsNotificationHandler(iface))
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
                .GroupBy(h => h.RequestType.ToGeneratedName())
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

            // Tell the user about any handler that is registered but gets no typed member, rather
            // than leaving its absence from IGeneratedRoutya unexplained.
            foreach (var handler in requestHandlers.Concat(notificationHandlers))
            {
                if (handler.SupportsTypedDispatch)
                    continue;

                var inaccessibleType = handler.RequestType.IsExternallyVisible()
                    ? handler.ResponseType!
                    : handler.RequestType;

                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TypedDispatchNotGenerated,
                    handler.HandlerType.Locations.FirstOrDefault() ?? Location.None,
                    handler.HandlerType.Name,
                    inaccessibleType.ToDisplayString()));
            }

            // Generate the registration code. Every discovered handler is registered, including
            // those without a typed member.
            var source = HandlerRegistrationEmitter.Generate(requestHandlers, notificationHandlers);
            context.AddSource("RoutyaGenerated.Registration.g.cs", source);

            // Generate the optimized dispatcher. Only handlers whose types can appear in a public
            // signature get a typed method.
            var notificationGroups = notificationHandlers
                .Where(h => h.SupportsTypedDispatch)
                .GroupBy(h => h.RequestType.ToGeneratedName())
                .ToDictionary(g => g.Key, g => g.ToList());

            var dispatcherSource = DispatcherEmitter.EmitGeneratedDispatcher(
                requestHandlers.Where(h => h.SupportsTypedDispatch).ToList(),
                notificationGroups);
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
                RequestType = typeArgs[0],
                ResponseType = typeArgs[1],
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
                RequestType = typeArgs[0],
                ResponseType = null,
                IsAsync = true, // Notification handlers are always async
                IsNotification = true,
                Lifetime = DetectLifetime(handler),
                HandlerInterfaceName = INotificationHandlerName
            };
        }

        /// <summary>
        /// Finds Routya handlers in referenced assemblies, which the syntax provider cannot see
        /// because they have no syntax tree in this compilation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is what makes a multiple project solution work. In the common Clean Architecture
        /// layout the handlers live in an Application project and the generator runs in the Web or
        /// API project, so without this every handler is invisible.
        /// </para>
        /// <para>
        /// Only assemblies that themselves reference Routya.Core are walked. Everything else,
        /// including the whole base class library, is skipped on an assembly identity check before
        /// any type is enumerated, which keeps the cost proportional to the number of projects that
        /// actually use Routya rather than to the size of the reference closure.
        /// </para>
        /// </remarks>
        private static IEnumerable<INamedTypeSymbol> GetHandlersFromReferencedAssemblies(
            Compilation compilation,
            SourceProductionContext context)
        {
            var found = new List<INamedTypeSymbol>();

            foreach (var reference in compilation.References)
            {
                if (!(compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly))
                    continue;

                if (!ReferencesRoutyaCore(assembly))
                    continue;

                foreach (var type in GetAllTypes(assembly.GlobalNamespace))
                {
                    if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                        continue;

                    if (!ImplementsAnyHandlerInterface(type))
                        continue;

                    // Generated registration lives in the consuming assembly, so a handler that is
                    // not visible from here cannot be referenced by it. Say so rather than leaving
                    // the user to find a missing service at runtime.
                    if (!type.IsExternallyVisible())
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.ReferencedHandlerNotAccessible,
                            Location.None,
                            type.Name,
                            assembly.Name));

                        continue;
                    }

                    found.Add(type);
                }
            }

            return found;
        }

        /// <summary>
        /// Whether an assembly references Routya.Core, and so could contain handlers.
        /// </summary>
        private static bool ReferencesRoutyaCore(IAssemblySymbol assembly)
        {
            // Routya.Core itself declares the interfaces but no handlers
            if (assembly.Name == RoutyaCoreAssemblyName)
                return false;

            foreach (var module in assembly.Modules)
            {
                foreach (var referenced in module.ReferencedAssemblies)
                {
                    if (referenced.Name == RoutyaCoreAssemblyName)
                        return true;
                }
            }

            return false;
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol root)
        {
            foreach (var type in root.GetTypeMembers())
            {
                foreach (var nested in GetTypeAndNested(type))
                {
                    yield return nested;
                }
            }

            foreach (var child in root.GetNamespaceMembers())
            {
                foreach (var type in GetAllTypes(child))
                {
                    yield return type;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetTypeAndNested(INamedTypeSymbol type)
        {
            yield return type;

            foreach (var nested in type.GetTypeMembers())
            {
                foreach (var inner in GetTypeAndNested(nested))
                {
                    yield return inner;
                }
            }
        }

        private static bool ImplementsAnyHandlerInterface(INamedTypeSymbol type)
        {
            foreach (var iface in type.AllInterfaces)
            {
                if (IsRequestHandler(iface) || IsAsyncRequestHandler(iface)
                    || IsNotificationHandler(iface) || IsStreamRequestHandler(iface))
                {
                    return true;
                }
            }

            return false;
        }

        private static ServiceLifetime DetectLifetime(INamedTypeSymbol handler)
        {
            // Look for lifetime attributes or conventions
            // Default to Transient for stateless handlers (matches MediatR behavior)
            // Transient handlers are created per call but don't show in allocation tracking
            // since they're short-lived and immediately eligible for GC
            return ServiceLifetime.Transient;
        }

    }
}
