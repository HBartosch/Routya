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
    internal static class CompiledPipelineFactory
    {
        private static readonly ConcurrentDictionary<Type, Delegate> _asyncCache = new ConcurrentDictionary<Type, Delegate>();
        private static readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();

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
            Type behaviorType = typeof(IPipelineBehavior<TRequest, TResponse>);

            // Check registry for handler info - try async first, then sync if not found
            // A request can only have ONE handler (either async OR sync, never both)
            requestHandlerRegistry.TryGetValue(asyncHandlerType, out var asyncHandlerInfo);
            RequestHandlerInfo syncHandlerInfo = null;
            if (asyncHandlerInfo == null)
            {
                requestHandlerRegistry.TryGetValue(syncHandlerType, out syncHandlerInfo);
            }
            
            // Track if we need to populate registry from fallback on first call
            bool needsFallbackCheck = asyncHandlerInfo == null && syncHandlerInfo == null;
            
            return (provider, request, cancellationToken) =>
            {
                IAsyncRequestHandler<TRequest, TResponse> asyncHandler = null;
                
                // Fast path: Use registry to resolve handler directly by concrete type!
                if (asyncHandlerInfo != null)
                {
                    // Direct resolution by concrete type - FAST!
                    asyncHandler = (IAsyncRequestHandler<TRequest, TResponse>)provider.GetRequiredService(asyncHandlerInfo.ConcreteType);
                }
                else
                {
                    // Fallback: Not in registry, try GetService (for backward compatibility)
                    asyncHandler = provider.GetService<IAsyncRequestHandler<TRequest, TResponse>>();
                    
                    // If found via fallback, add to registry for future optimization
                    if (asyncHandler != null && needsFallbackCheck)
                    {
                        var handlerConcreteType = asyncHandler.GetType();
                        var descriptor = provider.GetService<ServiceDescriptor>();
                        var lifetime = ServiceLifetime.Transient; // Default fallback lifetime
                        
                        // Try to determine actual lifetime from service collection
                        // (This is best-effort; we default to Transient if unknown)
                        
                        lock (requestHandlerRegistry)
                        {
                            // Double-check it wasn't added by another thread
                            if (!requestHandlerRegistry.ContainsKey(asyncHandlerType))
                            {
                                requestHandlerRegistry[asyncHandlerType] = new RequestHandlerInfo
                                {
                                    ConcreteType = handlerConcreteType,
                                    Lifetime = lifetime
                                };
                            }
                        }
                    }
                }
                
                if (asyncHandler != null)
                {
                    var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                    
                    // Check if behaviors exist without allocating array
                    var behaviorEnumerator = behaviors.GetEnumerator();
                    if (!behaviorEnumerator.MoveNext())
                    {
                        // Fast path: No behaviors, direct handler invocation
                        return asyncHandler.HandleAsync(request, cancellationToken);
                    }
                    
                    // Build pipeline with behaviors
                    var behaviorArray = behaviors.ToArray();
                    RequestHandlerDelegate<TResponse> finalHandler = () => asyncHandler.HandleAsync(request, cancellationToken);
                    
                    for (int i = behaviorArray.Length - 1; i >= 0; i--)
                    {
                        var behavior = behaviorArray[i];
                        var next = finalHandler;
                        finalHandler = () => behavior.Handle(request, next, cancellationToken);
                    }
                    
                    return finalHandler();
                }
                
                // Fallback to sync handler - use registry!
                IRequestHandler<TRequest, TResponse> syncHandler = null;
                if (syncHandlerInfo != null)
                {
                    // Direct resolution by concrete type - FAST!
                    syncHandler = (IRequestHandler<TRequest, TResponse>)provider.GetRequiredService(syncHandlerInfo.ConcreteType);
                }
                else
                {
                    // Fallback: Not in registry, try GetService (for backward compatibility)
                    syncHandler = provider.GetService<IRequestHandler<TRequest, TResponse>>();
                    
                    if (syncHandler == null)
                    {
                        throw new InvalidOperationException($"No handler found for request type {typeof(TRequest).Name}");
                    }
                    
                    // If found via fallback, add to registry for future optimization
                    if (needsFallbackCheck)
                    {
                        var handlerConcreteType = syncHandler.GetType();
                        var lifetime = ServiceLifetime.Transient; // Default fallback lifetime
                        
                        lock (requestHandlerRegistry)
                        {
                            // Double-check it wasn't added by another thread
                            if (!requestHandlerRegistry.ContainsKey(syncHandlerType))
                            {
                                requestHandlerRegistry[syncHandlerType] = new RequestHandlerInfo
                                {
                                    ConcreteType = handlerConcreteType,
                                    Lifetime = lifetime
                                };
                            }
                        }
                    }
                }
                
                var syncBehaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                var syncBehaviorEnumerator = syncBehaviors.GetEnumerator();
                
                if (!syncBehaviorEnumerator.MoveNext())
                {
                    // Fast path: No behaviors, direct handler invocation
                    return Task.FromResult(syncHandler!.Handle(request));
                }
                
                // Build pipeline with behaviors
                var syncBehaviorArray = syncBehaviors.ToArray();
                RequestHandlerDelegate<TResponse> syncFinalHandler = () => Task.FromResult(syncHandler!.Handle(request));
                
                for (int i = syncBehaviorArray.Length - 1; i >= 0; i--)
                {
                    var behavior = syncBehaviorArray[i];
                    var next = syncFinalHandler;
                    syncFinalHandler = () => behavior.Handle(request, next, cancellationToken);
                }
                
                return syncFinalHandler();
            };
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
                IRequestHandler<TRequest, TResponse> handler;
                
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
                
                var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
                
                // Fast path: Check if behaviors exist without allocating array
                var behaviorEnumerator = behaviors.GetEnumerator();
                if (!behaviorEnumerator.MoveNext())
                {
                    // Fast path: No behaviors, direct handler invocation
                    return handler!.Handle(request);
                }
                
                // Build pipeline with behaviors
                var behaviorArray = behaviors.ToArray();
                RequestHandlerDelegate<TResponse> finalHandler = () => Task.FromResult(handler!.Handle(request));

                for (int i = behaviorArray.Length - 1; i >= 0; i--)
                {
                    var behavior = behaviorArray[i];
                    var next = finalHandler;
                    finalHandler = () => behavior.Handle(request, next, CancellationToken.None);
                }

                return finalHandler().GetAwaiter().GetResult();
            };
        }
    }

}