using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Requests;
using Routya.Core.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Pipelines
{
    /// <remarks>
    /// One instance per <see cref="Requests.CompiledRequestInvokerDispatcher"/>, and therefore one per
    /// DI container. The caches below must never be static: a static cache keyed on request type is
    /// shared by every container in the process, which leaks state between containers (for example
    /// between two WebApplicationFactory hosts in the same test run).
    /// </remarks>
    internal sealed class CompiledPipelineFactory
    {
        private readonly Dictionary<Type, RequestHandlerInfo> _requestHandlerRegistry;

        private readonly ConcurrentDictionary<Type, Delegate> _asyncCache = new ConcurrentDictionary<Type, Delegate>();
        private readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();

        // Records whether any IPipelineBehavior is registered for a given request type. Service
        // registrations are immutable once the container is built, so this is a container wide fact
        // and safe to cache. The resolved behavior *instances* are deliberately never cached: a
        // behavior may be Scoped, so it must be resolved from the current dispatch scope every time.
        private readonly ConcurrentDictionary<Type, bool> _hasBehaviors = new ConcurrentDictionary<Type, bool>();

        public CompiledPipelineFactory(Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
        {
            _requestHandlerRegistry = requestHandlerRegistry;
        }

        /// <summary>
        /// Resolves the pipeline behaviors for a request from the supplied provider, which is the
        /// dispatch scope when <see cref="Extensions.RoutyaDispatchScope.Scoped"/> is configured.
        /// </summary>
        private IPipelineBehavior<TRequest, TResponse>[] ResolveBehaviors<TRequest, TResponse>(IServiceProvider provider)
        {
            // Fast path: this request type is already known to have no behaviors registered, so
            // there is nothing to resolve and we can skip the GetServices call entirely.
            if (_hasBehaviors.TryGetValue(typeof(TRequest), out var hasBehaviors) && !hasBehaviors)
            {
                return EmptyBehaviors<TRequest, TResponse>.Instance;
            }

            var resolved = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var behaviors = resolved as IPipelineBehavior<TRequest, TResponse>[] ?? resolved.ToArray();

            _hasBehaviors[typeof(TRequest)] = behaviors.Length > 0;

            return behaviors;
        }

        private static class EmptyBehaviors<TRequest, TResponse>
        {
            public static readonly IPipelineBehavior<TRequest, TResponse>[] Instance =
                new IPipelineBehavior<TRequest, TResponse>[0];
        }

        public Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> GetOrAdd<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>>)_asyncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledAsyncPipeline<TRequest, TResponse>(_requestHandlerRegistry));
        }

        private Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> BuildPrecompiledAsyncPipeline<TRequest, TResponse>(
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
            where TRequest : IRequest<TResponse>
        {
            // Pre-compile type information (happens once per request type)
            Type asyncHandlerType = typeof(IAsyncRequestHandler<TRequest, TResponse>);
            Type syncHandlerType = typeof(IRequestHandler<TRequest, TResponse>);

            // Check registry for handler info - try async first, then sync if not found
            requestHandlerRegistry.TryGetValue(asyncHandlerType, out var asyncHandlerInfo);
            RequestHandlerInfo? syncHandlerInfo = null;
            if (asyncHandlerInfo == null)
            {
                requestHandlerRegistry.TryGetValue(syncHandlerType, out syncHandlerInfo);
            }
            
            return (provider, request, cancellationToken) =>
            {
                // Resolve handler
                IAsyncRequestHandler<TRequest, TResponse>? asyncHandler = null;
                
                if (asyncHandlerInfo != null)
                {
                    asyncHandler = (IAsyncRequestHandler<TRequest, TResponse>)provider.GetRequiredService(asyncHandlerInfo.ConcreteType);
                }
                else
                {
                    asyncHandler = provider.GetService<IAsyncRequestHandler<TRequest, TResponse>>();
                }
                
                if (asyncHandler != null)
                {
                    // Resolve behaviors from the current dispatch scope, never from a cache
                    var behaviors = ResolveBehaviors<TRequest, TResponse>(provider);

                    if (behaviors.Length == 0)
                    {
                        // Fast path: No behaviors, direct handler invocation
                        return asyncHandler.HandleAsync(request, cancellationToken);
                    }
                    
                    // Build pre-compiled pipeline chain
                    return BuildCompiledAsyncChain(asyncHandler, behaviors, request, cancellationToken);
                }
                
                // Fallback to sync handler
                IRequestHandler<TRequest, TResponse>? syncHandler = null;
                if (syncHandlerInfo != null)
                {
                    syncHandler = (IRequestHandler<TRequest, TResponse>)provider.GetRequiredService(syncHandlerInfo.ConcreteType);
                }
                else
                {
                    syncHandler = provider.GetService<IRequestHandler<TRequest, TResponse>>();
                    
                    if (syncHandler == null)
                    {
                        throw new InvalidOperationException($"No handler found for request type {typeof(TRequest).Name}");
                    }
                    
                }
                
                var syncBehaviors = ResolveBehaviors<TRequest, TResponse>(provider);

                if (syncBehaviors.Length == 0)
                {
                    return Task.FromResult(syncHandler!.Handle(request));
                }
                
                return BuildCompiledSyncToAsyncChain(syncHandler, syncBehaviors, request, cancellationToken);
            };
        }
        
        private static Task<TResponse> BuildCompiledAsyncChain<TRequest, TResponse>(
            IAsyncRequestHandler<TRequest, TResponse> handler,
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            TRequest request,
            CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
        {
            // V2 OPTIMIZATION: Fast paths for common cases (0, 1, 2 behaviors)
            switch (behaviors.Length)
            {
                case 0:
                    // No behaviors - direct handler call
                    return handler.HandleAsync(request, cancellationToken);

                case 1:
                    // Single behavior - inline the call
                    return behaviors[0].Handle(
                        request,
                        ct => handler.HandleAsync(request, ct),
                        cancellationToken
                    );

                case 2:
                    // Two behaviors (most common in benchmarks) - nested inline
                    return behaviors[0].Handle(
                        request,
                        ct => behaviors[1].Handle(
                            request,
                            ct2 => handler.HandleAsync(request, ct2),
                            ct
                        ),
                        cancellationToken
                    );

                default:
                    // 3+ behaviors - use loop-based approach
                    RequestHandlerDelegate<TResponse> innerDelegate = (ct) => handler.HandleAsync(request, ct);

                    for (int i = behaviors.Length - 1; i >= 0; i--)
                    {
                        var currentBehavior = behaviors[i];
                        var previousDelegate = innerDelegate;
                        innerDelegate = (ct) => currentBehavior.Handle(request, previousDelegate, ct);
                    }

                    return innerDelegate(cancellationToken);
            }
        }
        
        private static Task<TResponse> BuildCompiledSyncToAsyncChain<TRequest, TResponse>(
            IRequestHandler<TRequest, TResponse> handler,
            IPipelineBehavior<TRequest, TResponse>[] behaviors,
            TRequest request,
            CancellationToken cancellationToken)
            where TRequest : IRequest<TResponse>
        {
            // Optimized: Execute behaviors directly without lambda allocations
            if (behaviors.Length == 1)
            {
                // Fast path for single behavior
                return behaviors[0].Handle(request, (ct) => Task.FromResult(handler.Handle(request)), cancellationToken);
            }
            
            if (behaviors.Length == 2)
            {
                // Fast path for two behaviors - most common case
                RequestHandlerDelegate<TResponse> innerDelegate = (ct) => Task.FromResult(handler.Handle(request));
                RequestHandlerDelegate<TResponse> middleDelegate = (ct) => behaviors[1].Handle(request, innerDelegate, ct);
                return behaviors[0].Handle(request, middleDelegate, cancellationToken);
            }
            
            // General case
            RequestHandlerDelegate<TResponse> current = (ct) => Task.FromResult(handler.Handle(request));
            
            for (int i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var next = current;
                current = (ct) => behavior.Handle(request, next, ct);
            }
            
            return current(cancellationToken);
        }

        public Func<IServiceProvider, TRequest, TResponse> GetOrAddSync<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, TResponse>)_syncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledSyncPipeline<TRequest, TResponse>(_requestHandlerRegistry));
        }

        private Func<IServiceProvider, TRequest, TResponse> BuildPrecompiledSyncPipeline<TRequest, TResponse>(
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
            where TRequest : IRequest<TResponse>
        {
            // Pre-compile type information (happens once per request type)
            Type handlerType = typeof(IRequestHandler<TRequest, TResponse>);

            // Check registry for handler info
            requestHandlerRegistry.TryGetValue(handlerType, out var handlerInfo);
            
            return (provider, request) =>
            {
                IRequestHandler<TRequest, TResponse>? handler;
                
                if (handlerInfo != null)
                {
                    // Direct resolution by concrete type - FAST!
                    handler = (IRequestHandler<TRequest, TResponse>)provider.GetRequiredService(handlerInfo.ConcreteType);
                }
                else
                {
                    // Fallback: Not in registry, try GetService (for backward compatibility)
                    handler = provider.GetService<IRequestHandler<TRequest, TResponse>>();
                    
                    if (handler == null)
                    {
                        throw new InvalidOperationException($"No handler found for request type {typeof(TRequest).Name}");
                    }
                    
                }
                
                // Resolve behaviors from the current dispatch scope, never from a cache
                var behaviors = ResolveBehaviors<TRequest, TResponse>(provider);

                if (behaviors.Length == 0)
                {
                    // Fast path: No behaviors, direct handler invocation
                    return handler!.Handle(request);
                }
                
                if (behaviors.Length == 1)
                {
                    // Fast path for single behavior
                    RequestHandlerDelegate<TResponse> handlerDelegate = (ct) => Task.FromResult(handler!.Handle(request));
                    return behaviors[0].Handle(request, handlerDelegate, CancellationToken.None).GetAwaiter().GetResult();
                }
                
                if (behaviors.Length == 2)
                {
                    // Fast path for two behaviors - most common case
                    RequestHandlerDelegate<TResponse> innerDelegate = (ct) => Task.FromResult(handler!.Handle(request));
                    RequestHandlerDelegate<TResponse> middleDelegate = (ct) => behaviors[1].Handle(request, innerDelegate, ct);
                    return behaviors[0].Handle(request, middleDelegate, CancellationToken.None).GetAwaiter().GetResult();
                }
                
                // General case: Build pipeline with behaviors
                RequestHandlerDelegate<TResponse> finalHandler = (ct) => Task.FromResult(handler!.Handle(request));

                for (int i = behaviors.Length - 1; i >= 0; i--)
                {
                    var behavior = behaviors[i];
                    var next = finalHandler;
                    finalHandler = (ct) => behavior.Handle(request, next, ct);
                }

                return finalHandler(CancellationToken.None).GetAwaiter().GetResult();
            };
        }
    }

}