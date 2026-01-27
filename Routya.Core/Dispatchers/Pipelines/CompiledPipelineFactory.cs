using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Requests;
using Routya.Core.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Pipelines
{
    internal static class CompiledPipelineFactory
    {
        private static readonly ConcurrentDictionary<Type, Delegate> _asyncCache = new ConcurrentDictionary<Type, Delegate>();
        private static readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();
        
        // Cache for pre-resolved behavior arrays (per request type)
        private static readonly ConcurrentDictionary<Type, object> _behaviorCache = new ConcurrentDictionary<Type, object>();

        public static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> GetOrAdd<TRequest, TResponse>(
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>>)_asyncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledAsyncPipeline<TRequest, TResponse>(requestHandlerRegistry));
        }

        private static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> BuildPrecompiledAsyncPipeline<TRequest, TResponse>(
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
            
            // Track if we need to populate registry from fallback on first call
            bool needsFallbackCheck = asyncHandlerInfo == null && syncHandlerInfo == null;
            
            // Build expression tree for the compiled pipeline
            var providerParam = Expression.Parameter(typeof(IServiceProvider), "provider");
            var requestParam = Expression.Parameter(typeof(TRequest), "request");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "cancellationToken");
            
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
                    
                    if (asyncHandler != null && needsFallbackCheck)
                    {
                        var handlerConcreteType = asyncHandler.GetType();
                        lock (requestHandlerRegistry)
                        {
                            if (!requestHandlerRegistry.ContainsKey(asyncHandlerType))
                            {
                                requestHandlerRegistry[asyncHandlerType] = new RequestHandlerInfo
                                {
                                    ConcreteType = handlerConcreteType,
                                    Lifetime = ServiceLifetime.Transient
                                };
                            }
                        }
                    }
                }
                
                if (asyncHandler != null)
                {
                    // Get or create cached behavior array for this request type
                    var behaviors = (IPipelineBehavior<TRequest, TResponse>[])_behaviorCache.GetOrAdd(
                        typeof(TRequest),
                        _ =>
                        {
                            var behaviorServices = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                            return behaviorServices as IPipelineBehavior<TRequest, TResponse>[] ?? behaviorServices.ToArray();
                        });
                    
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
                    
                    if (needsFallbackCheck)
                    {
                        var handlerConcreteType = syncHandler.GetType();
                        lock (requestHandlerRegistry)
                        {
                            if (!requestHandlerRegistry.ContainsKey(syncHandlerType))
                            {
                                requestHandlerRegistry[syncHandlerType] = new RequestHandlerInfo
                                {
                                    ConcreteType = handlerConcreteType,
                                    Lifetime = ServiceLifetime.Transient
                                };
                            }
                        }
                    }
                }
                
                var syncBehaviors = (IPipelineBehavior<TRequest, TResponse>[])_behaviorCache.GetOrAdd(
                    typeof(TRequest),
                    _ =>
                    {
                        var behaviorServices = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                        return behaviorServices as IPipelineBehavior<TRequest, TResponse>[] ?? behaviorServices.ToArray();
                    });
                
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

        public static Func<IServiceProvider, TRequest, TResponse> GetOrAddSync<TRequest, TResponse>(
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, TResponse>)_syncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledSyncPipeline<TRequest, TResponse>(requestHandlerRegistry));
        }

        private static Func<IServiceProvider, TRequest, TResponse> BuildPrecompiledSyncPipeline<TRequest, TResponse>(
            Dictionary<Type, RequestHandlerInfo> requestHandlerRegistry)
            where TRequest : IRequest<TResponse>
        {
            // Pre-compile type information (happens once per request type)
            Type handlerType = typeof(IRequestHandler<TRequest, TResponse>);

            // Check registry for handler info
            requestHandlerRegistry.TryGetValue(handlerType, out var handlerInfo);
            
            // Track if we need to populate registry from fallback on first call
            bool needsFallbackCheck = handlerInfo == null;
            
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
                    
                    // If found via fallback, add to registry for future optimization
                    if (needsFallbackCheck)
                    {
                        var handlerConcreteType = handler.GetType();
                        var lifetime = ServiceLifetime.Transient; // Default fallback lifetime
                        
                        lock (requestHandlerRegistry)
                        {
                            // Double-check it wasn't added by another thread
                            if (!requestHandlerRegistry.ContainsKey(handlerType))
                            {
                                requestHandlerRegistry[handlerType] = new RequestHandlerInfo
                                {
                                    ConcreteType = handlerConcreteType,
                                    Lifetime = lifetime
                                };
                            }
                        }
                    }
                }
                
                // Get or create cached behavior array for this request type
                var behaviors = (IPipelineBehavior<TRequest, TResponse>[])_behaviorCache.GetOrAdd(
                    typeof(TRequest),
                    _ =>
                    {
                        var behaviorServices = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                        return behaviorServices as IPipelineBehavior<TRequest, TResponse>[] ?? behaviorServices.ToArray();
                    });
                
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