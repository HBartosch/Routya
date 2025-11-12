using Microsoft.Extensions.DependencyInjection;
using Routya.Core.Abstractions;
using Routya.Core.Dispatchers.Requests;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Routya.Core.Dispatchers.Pipelines
{
    internal static class CompiledPipelineFactory
    {
        private static readonly ConcurrentDictionary<Type, Delegate> _asyncCache = new ConcurrentDictionary<Type, Delegate>();
        private static readonly ConcurrentDictionary<Type, Delegate> _syncCache = new ConcurrentDictionary<Type, Delegate>();

        public static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> GetOrAdd<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>>)_asyncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledAsyncPipeline<TRequest, TResponse>());
        }

        private static Func<IServiceProvider, TRequest, CancellationToken, Task<TResponse>> BuildPrecompiledAsyncPipeline<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            // Pre-compile type information (happens once per request type)
            Type asyncHandlerType = typeof(IAsyncRequestHandler<TRequest, TResponse>);
            Type syncHandlerType = typeof(IRequestHandler<TRequest, TResponse>);
            Type behaviorType = typeof(IPipelineBehavior<TRequest, TResponse>);
            
            return (provider, request, cancellationToken) =>
            {
                // Fast path: Try async handler first (most common case)
                var asyncHandler = provider.GetService(asyncHandlerType) as IAsyncRequestHandler<TRequest, TResponse>;
                
                if (asyncHandler != null)
                {
                    // No behaviors check - avoid GetServices call if possible
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
                
                // Fallback to sync handler
                var syncHandler = provider.GetRequiredService(syncHandlerType) as IRequestHandler<TRequest, TResponse>;
                
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

        public static Func<IServiceProvider, TRequest, TResponse> GetOrAddSync<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            return (Func<IServiceProvider, TRequest, TResponse>)_syncCache.GetOrAdd(typeof(TRequest), _ =>
                BuildPrecompiledSyncPipeline<TRequest, TResponse>());
        }

        private static Func<IServiceProvider, TRequest, TResponse> BuildPrecompiledSyncPipeline<TRequest, TResponse>()
            where TRequest : IRequest<TResponse>
        {
            // Pre-compile type information (happens once per request type)
            Type handlerType = typeof(IRequestHandler<TRequest, TResponse>);
            
            return (provider, request) =>
            {
                var handler = provider.GetRequiredService(handlerType) as IRequestHandler<TRequest, TResponse>;
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